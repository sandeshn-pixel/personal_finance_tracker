using FinanceTracker.Application.Goals.DTOs;
using FinanceTracker.Application.Notifications.Interfaces;
using FinanceTracker.Application.RecurringTransactions.DTOs;
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
        var service = new GoalService(dbContext, notificationService);
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
    public async Task GoalCompletion_PublishesNotification()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        await dbContext.SaveChangesAsync();

        INotificationService notificationService = new NotificationService(dbContext);
        var service = new GoalService(dbContext, notificationService);
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
        var recurringService = new RecurringTransactionService(dbContext, new TransactionService(dbContext, new CategorySeeder(dbContext)), notificationService);
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
    public async Task AutomationRun_CreatesReminderForManualRecurringRule()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var account = TestData.AddAccount(dbContext, user.Id, "Checking", 1000m);
        var category = TestData.AddCategory(dbContext, user.Id, "Rent", CategoryType.Expense);
        await dbContext.SaveChangesAsync();

        INotificationService notificationService = new NotificationService(dbContext);
        var recurringService = new RecurringTransactionService(dbContext, new TransactionService(dbContext, new CategorySeeder(dbContext)), notificationService);
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
            Options.Create(new AutomationOptions { PollingIntervalSeconds = 60, GoalReminderLookaheadDays = 7 }),
            NullLogger<AutomationService>.Instance);

        var result = await automationService.RunAsync(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None);
        var rule = await dbContext.RecurringTransactionRules.SingleAsync();
        var execution = await dbContext.RecurringTransactionExecutions.SingleAsync();
        var notification = await dbContext.UserNotifications.SingleAsync();

        Assert.Equal(1, result.ManualRemindersCreated);
        Assert.Equal(RecurringExecutionStatus.Reminded, execution.Status);
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc), rule.NextRunDateUtc);
        Assert.Equal(NotificationType.RecurringDueReminder, notification.Type);
    }
}