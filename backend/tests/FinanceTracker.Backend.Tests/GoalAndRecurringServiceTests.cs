using FinanceTracker.Application.Automation.DTOs;
using FinanceTracker.Application.Goals.DTOs;
using FinanceTracker.Application.Notifications.Interfaces;
using FinanceTracker.Application.RecurringTransactions.DTOs;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Automation;
using FinanceTracker.Infrastructure.Financial;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Backend.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Backend.Tests;

public sealed class GoalAndRecurringServiceTests
{
    [Fact]
    public async Task GoalContributionAndWithdrawal_UpdateGoalAndLinkedAccount()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var account = TestData.AddAccount(dbContext, user.Id, "Savings", 500m, AccountType.SavingsAccount);
        await dbContext.SaveChangesAsync();

        INotificationService notificationService = new NotificationService(dbContext);
        var service = CreateGoalService(dbContext, notificationService);
        var goal = await service.CreateAsync(user.Id, new CreateGoalRequest
        {
            Name = "Emergency Fund",
            TargetAmount = 1000m,
            LinkedAccountId = account.Id,
            Color = "#00ADB5",
            Icon = "Shield"
        }, CancellationToken.None);

        await service.RecordContributionAsync(user.Id, goal.Id, new RecordGoalEntryRequest { Amount = 200m }, CancellationToken.None);
        await service.RecordWithdrawalAsync(user.Id, goal.Id, new RecordGoalEntryRequest { Amount = 50m }, CancellationToken.None);

        var reloadedGoal = await service.GetAsync(user.Id, goal.Id, CancellationToken.None);
        var reloadedAccount = await dbContext.Accounts.SingleAsync(x => x.Id == account.Id);

        Assert.NotNull(reloadedGoal);
        Assert.Equal(150m, reloadedGoal!.Goal.CurrentAmount);
        Assert.Equal(350m, reloadedAccount.CurrentBalance);
        Assert.Equal(2, reloadedGoal.Entries.Count);
    }

    [Fact]
    public async Task GoalService_ListAsync_IncludesSharedViewerLinkedGoals_ReadOnly()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var owner = TestData.AddUser(dbContext, "owner@example.com");
        var viewer = TestData.AddUser(dbContext, "viewer@example.com");
        var account = TestData.AddAccount(dbContext, owner.Id, "Shared Savings", 1200m, AccountType.SavingsAccount);
        dbContext.AccountMemberships.Add(new AccountMembership
        {
            AccountId = account.Id,
            UserId = viewer.Id,
            Role = AccountMemberRole.Viewer,
            InvitedByUserId = owner.Id,
            LastModifiedByUserId = owner.Id
        });
        await dbContext.SaveChangesAsync();

        INotificationService notificationService = new NotificationService(dbContext);
        var service = CreateGoalService(dbContext, notificationService);
        await service.CreateAsync(owner.Id, new CreateGoalRequest
        {
            Name = "Family Vacation",
            TargetAmount = 5000m,
            LinkedAccountId = account.Id,
            Icon = "Plane",
            Color = "#C08552"
        }, CancellationToken.None);

        var goals = await service.ListAsync(viewer.Id, CancellationToken.None);

        var sharedGoal = Assert.Single(goals);
        Assert.Equal("Family Vacation", sharedGoal.Name);
        Assert.Equal(account.Id, sharedGoal.LinkedAccountId);
    }

    [Fact]
    public async Task GoalCompletion_PublishesNotification()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        await dbContext.SaveChangesAsync();

        INotificationService notificationService = new NotificationService(dbContext);
        var service = CreateGoalService(dbContext, notificationService);
        var goal = await service.CreateAsync(user.Id, new CreateGoalRequest
        {
            Name = "Laptop Fund",
            TargetAmount = 100m,
        }, CancellationToken.None);

        await service.RecordContributionAsync(user.Id, goal.Id, new RecordGoalEntryRequest { Amount = 100m }, CancellationToken.None);

        var notification = await dbContext.UserNotifications.SingleAsync();
        Assert.Equal(NotificationType.GoalCompleted, notification.Type);
        Assert.Contains("Laptop Fund", notification.Title);
    }

    [Fact]
    public async Task ProcessDue_IsIdempotentForTheSameOccurrence()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var account = TestData.AddAccount(dbContext, user.Id, "Checking", 1000m);
        var category = TestData.AddCategory(dbContext, user.Id, "Utilities", CategoryType.Expense);
        await dbContext.SaveChangesAsync();

        INotificationService notificationService = new NotificationService(dbContext);
        var recurringService = CreateRecurringService(dbContext, notificationService);
        await recurringService.CreateAsync(user.Id, new CreateRecurringTransactionRequest
        {
            Title = "Monthly utilities",
            Type = TransactionType.Expense,
            Amount = 100m,
            CategoryId = category.Id,
            AccountId = account.Id,
            Frequency = RecurringFrequency.Monthly,
            StartDateUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            AutoCreateTransaction = true
        }, CancellationToken.None);

        var firstRun = await recurringService.ProcessDueAsync(user.Id, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);
        var secondRun = await recurringService.ProcessDueAsync(user.Id, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);

        Assert.Equal(1, firstRun.TransactionsCreated);
        Assert.Equal(0, secondRun.TransactionsCreated);
        Assert.Equal(1, await dbContext.Transactions.CountAsync(x => x.RecurringTransactionId != null));
        Assert.Equal(1, await dbContext.RecurringTransactionExecutions.CountAsync());
    }

    [Fact]
    public async Task RecurringTransactionService_ListAsync_IncludesSharedViewerLinkedRules_ReadOnly()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var owner = TestData.AddUser(dbContext, "owner@example.com");
        var viewer = TestData.AddUser(dbContext, "viewer@example.com");
        var account = TestData.AddAccount(dbContext, owner.Id, "Shared Checking", 2200m);
        var category = TestData.AddCategory(dbContext, owner.Id, "Rent", CategoryType.Expense);
        dbContext.AccountMemberships.Add(new AccountMembership
        {
            AccountId = account.Id,
            UserId = viewer.Id,
            Role = AccountMemberRole.Viewer,
            InvitedByUserId = owner.Id,
            LastModifiedByUserId = owner.Id
        });
        await dbContext.SaveChangesAsync();

        INotificationService notificationService = new NotificationService(dbContext);
        var recurringService = CreateRecurringService(dbContext, notificationService);
        await recurringService.CreateAsync(owner.Id, new CreateRecurringTransactionRequest
        {
            Title = "Shared rent",
            Type = TransactionType.Expense,
            Amount = 900m,
            CategoryId = category.Id,
            AccountId = account.Id,
            Frequency = RecurringFrequency.Monthly,
            StartDateUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            AutoCreateTransaction = false
        }, CancellationToken.None);

        var rules = await recurringService.ListAsync(viewer.Id, CancellationToken.None);

        var sharedRule = Assert.Single(rules);
        Assert.Equal("Shared rent", sharedRule.Title);
        Assert.Equal(account.Id, sharedRule.AccountId);
    }

    [Fact]
    public async Task ProcessDue_FailedExecution_SchedulesRetry_AndStopsAfterMaxAttempts()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var account = TestData.AddAccount(dbContext, user.Id, "Checking", 1000m);
        var category = TestData.AddCategory(dbContext, user.Id, "Rent", CategoryType.Expense);
        await dbContext.SaveChangesAsync();

        INotificationService notificationService = new NotificationService(dbContext);
        var recurringService = CreateRecurringService(dbContext, notificationService, new AutomationOptions
        {
            MaxRecurringRetryAttempts = 3,
            InitialRetryDelaySeconds = 15,
            MaxRetryDelaySeconds = 60
        });

        await recurringService.CreateAsync(user.Id, new CreateRecurringTransactionRequest
        {
            Title = "Monthly rent",
            Type = TransactionType.Expense,
            Amount = 800m,
            CategoryId = category.Id,
            AccountId = account.Id,
            Frequency = RecurringFrequency.Monthly,
            StartDateUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            AutoCreateTransaction = true
        }, CancellationToken.None);

        account.IsArchived = true;
        await dbContext.SaveChangesAsync();

        var firstRun = await recurringService.ProcessDueAsync(user.Id, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);
        var execution = await dbContext.RecurringTransactionExecutions.SingleAsync();

        Assert.Equal(1, firstRun.OccurrencesDeferredForRetry);
        Assert.Equal(1, execution.AttemptCount);
        Assert.NotNull(execution.NextRetryAfterUtc);
        Assert.Equal(1, await dbContext.UserNotifications.CountAsync(x => x.Type == NotificationType.RecurringExecutionFailed));

        var secondRunWhileBackingOff = await recurringService.ProcessDueAsync(user.Id, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);
        execution = await dbContext.RecurringTransactionExecutions.SingleAsync();

        Assert.Equal(1, secondRunWhileBackingOff.OccurrencesDeferredForRetry);
        Assert.Equal(1, execution.AttemptCount);

        execution.NextRetryAfterUtc = DateTime.UtcNow.AddSeconds(-1);
        await dbContext.SaveChangesAsync();
        var secondAttempt = await recurringService.ProcessDueAsync(user.Id, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);

        execution = await dbContext.RecurringTransactionExecutions.SingleAsync();
        Assert.Equal(1, secondAttempt.OccurrencesDeferredForRetry);
        Assert.Equal(2, execution.AttemptCount);
        Assert.NotNull(execution.NextRetryAfterUtc);
        Assert.Equal(1, await dbContext.UserNotifications.CountAsync(x => x.Type == NotificationType.RecurringExecutionFailed));

        execution.NextRetryAfterUtc = DateTime.UtcNow.AddSeconds(-1);
        await dbContext.SaveChangesAsync();
        var thirdAttempt = await recurringService.ProcessDueAsync(user.Id, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);

        execution = await dbContext.RecurringTransactionExecutions.SingleAsync();
        Assert.Equal(1, thirdAttempt.OccurrencesFailedPermanently);
        Assert.Equal(3, execution.AttemptCount);
        Assert.Null(execution.NextRetryAfterUtc);
        Assert.Equal(2, await dbContext.UserNotifications.CountAsync(x => x.Type == NotificationType.RecurringExecutionFailed));
    }

    [Fact]
    public async Task AutomationRun_CreatesReminderForManualRecurringRule()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var account = TestData.AddAccount(dbContext, user.Id, "Checking", 1000m);
        var category = TestData.AddCategory(dbContext, user.Id, "Rent", CategoryType.Expense);
        await dbContext.SaveChangesAsync();

        INotificationService notificationService = new NotificationService(dbContext);
        var recurringService = CreateRecurringService(dbContext, notificationService);
        await recurringService.CreateAsync(user.Id, new CreateRecurringTransactionRequest
        {
            Title = "Monthly rent reminder",
            Type = TransactionType.Expense,
            Amount = 800m,
            CategoryId = category.Id,
            AccountId = account.Id,
            Frequency = RecurringFrequency.Monthly,
            StartDateUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            AutoCreateTransaction = false
        }, CancellationToken.None);

        var automationService = new AutomationService(
            dbContext,
            recurringService,
            notificationService,
            Options.Create(new AutomationOptions { PollingIntervalSeconds = 60, GoalReminderLookaheadDays = 7, MaxRecurringRetryAttempts = 3, InitialRetryDelaySeconds = 60, MaxRetryDelaySeconds = 900 }),
            NullLogger<AutomationService>.Instance);

        var result = await automationService.RunAsync(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);
        var rule = await dbContext.RecurringTransactionRules.SingleAsync();
        var execution = await dbContext.RecurringTransactionExecutions.SingleAsync();
        var notification = await dbContext.UserNotifications.SingleAsync();

        Assert.Equal(1, result.ManualRemindersCreated);
        Assert.Equal(RecurringExecutionStatus.Reminded, execution.Status);
        Assert.Equal(1, execution.AttemptCount);
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), rule.NextRunDateUtc);
        Assert.Equal(NotificationType.RecurringDueReminder, notification.Type);
    }

    [Fact]
    public void AutomationStatusTracker_TracksFailuresSuccessAndNextAttempt()
    {
        var tracker = new AutomationStatusTracker();
        var startedUtc = new DateTime(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc);
        var firstRetryUtc = startedUtc.AddMinutes(2);
        var secondRetryUtc = startedUtc.AddMinutes(5);
        var successUtc = startedUtc.AddMinutes(8);
        var nextPlannedUtc = startedUtc.AddMinutes(9);

        tracker.RecordStarted(startedUtc);
        var firstFailureCount = tracker.RecordFailed(startedUtc.AddMinutes(1), "First failure", firstRetryUtc);
        var secondFailureCount = tracker.RecordFailed(startedUtc.AddMinutes(4), "Second failure", secondRetryUtc);
        tracker.RecordStarted(startedUtc.AddMinutes(7));
        tracker.RecordSucceeded(new AutomationRunSummaryDto(1, 2, 2, 1, 0, 1, 0, successUtc), successUtc, nextPlannedUtc);

        var snapshot = tracker.GetSnapshot(true, 60);

        Assert.Equal(1, firstFailureCount);
        Assert.Equal(2, secondFailureCount);
        Assert.True(snapshot.BackgroundProcessingEnabled);
        Assert.True(snapshot.LastRunSucceeded);
        Assert.False(snapshot.IsCycleRunning);
        Assert.Equal(0, snapshot.ConsecutiveFailureCount);
        Assert.Equal(2, snapshot.TotalFailureCount);
        Assert.Equal(successUtc, snapshot.LastSuccessfulCompletedUtc);
        Assert.Equal(nextPlannedUtc, snapshot.NextAttemptUtc);
        Assert.NotNull(snapshot.LastSummary);
        Assert.Equal(1, snapshot.LastSummary!.AutoOccurrencesDeferredForRetry);
    }

    private static GoalService CreateGoalService(
        FinanceTracker.Infrastructure.Persistence.ApplicationDbContext dbContext,
        INotificationService notificationService)
        => new(dbContext, notificationService, new AccountAccessService(dbContext));

    private static RecurringTransactionService CreateRecurringService(
        FinanceTracker.Infrastructure.Persistence.ApplicationDbContext dbContext,
        INotificationService notificationService,
        AutomationOptions? options = null)
        => new(
            dbContext,
            new TransactionService(dbContext, new CategorySeeder(dbContext), new AccountAccessService(dbContext)),
            notificationService,
            Options.Create(options ?? new AutomationOptions
            {
                PollingIntervalSeconds = 60,
                GoalReminderLookaheadDays = 7,
                MaxRecurringRetryAttempts = 3,
                InitialRetryDelaySeconds = 60,
                MaxRetryDelaySeconds = 900
            }),
            new AccountAccessService(dbContext));
}
