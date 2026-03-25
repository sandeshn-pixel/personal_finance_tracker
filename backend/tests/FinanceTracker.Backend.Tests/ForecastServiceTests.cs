using FinanceTracker.Application.Forecasting.DTOs;
using FinanceTracker.Backend.Tests.TestSupport;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Financial;
using FinanceTracker.Infrastructure.Forecasting;

namespace FinanceTracker.Backend.Tests;

public sealed class ForecastServiceTests
{
    [Fact]
    public async Task MonthForecast_ExcludesTransfersAndIncludesUpcomingRecurringExpense()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var checking = TestData.AddAccount(dbContext, user.Id, "Checking", 1000m);
        var savings = TestData.AddAccount(dbContext, user.Id, "Savings", 500m, AccountType.SavingsAccount);

        dbContext.Transactions.AddRange(
            CreateTransaction(user.Id, checking.Id, TransactionType.Income, 900m, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, checking.Id, TransactionType.Income, 900m, new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, checking.Id, TransactionType.Expense, 300m, new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, checking.Id, TransactionType.Expense, 300m, new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, checking.Id, TransactionType.Expense, 300m, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, checking.Id, TransactionType.Expense, 300m, new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, checking.Id, TransactionType.Transfer, 700m, new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), savings.Id));

        dbContext.RecurringTransactionRules.Add(new RecurringTransactionRule
        {
            UserId = user.Id,
            Title = "Rent",
            Type = TransactionType.Expense,
            Amount = 200m,
            AccountId = checking.Id,
            Frequency = RecurringFrequency.Monthly,
            StartDateUtc = new DateTime(2026, 3, 25, 0, 0, 0, DateTimeKind.Utc),
            NextRunDateUtc = new DateTime(2026, 3, 25, 0, 0, 0, DateTimeKind.Utc),
            Status = RecurringRuleStatus.Active
        });

        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, new DateTimeOffset(2026, 3, 21, 8, 0, 0, TimeSpan.Zero));
        var summary = await service.GetMonthSummaryAsync(user.Id, new ForecastQuery(null, null), CancellationToken.None);

        Assert.Equal(6.67m, summary.AverageDailyNet);
        Assert.Equal(200m, summary.UpcomingRecurring.TotalExpectedExpense);
        Assert.Single(summary.UpcomingRecurring.Items);
        Assert.Equal(ForecastRiskLevel.Low, summary.RiskLevel);
    }

    [Fact]
    public async Task MonthForecast_DoesNotDoubleCountRecurringTransactionAlreadyMaterialized()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var checking = TestData.AddAccount(dbContext, user.Id, "Checking", 800m);
        var ruleId = Guid.NewGuid();

        dbContext.RecurringTransactionRules.Add(new RecurringTransactionRule
        {
            Id = ruleId,
            UserId = user.Id,
            Title = "Salary",
            Type = TransactionType.Income,
            Amount = 500m,
            AccountId = checking.Id,
            Frequency = RecurringFrequency.Monthly,
            StartDateUtc = new DateTime(2026, 3, 25, 0, 0, 0, DateTimeKind.Utc),
            NextRunDateUtc = new DateTime(2026, 3, 25, 0, 0, 0, DateTimeKind.Utc),
            Status = RecurringRuleStatus.Active
        });

        dbContext.Transactions.Add(new Transaction
        {
            UserId = user.Id,
            AccountId = checking.Id,
            Type = TransactionType.Income,
            Amount = 500m,
            DateUtc = new DateTime(2026, 3, 25, 0, 0, 0, DateTimeKind.Utc),
            RecurringTransactionId = ruleId,
            CreatedByUserId = user.Id,
            UpdatedByUserId = user.Id
        });

        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, new DateTimeOffset(2026, 3, 21, 8, 0, 0, TimeSpan.Zero));
        var summary = await service.GetMonthSummaryAsync(user.Id, new ForecastQuery(null, null), CancellationToken.None);

        Assert.Equal(0m, summary.UpcomingRecurring.TotalExpectedIncome);
        Assert.Empty(summary.UpcomingRecurring.Items);
    }

    [Fact]
    public async Task MonthForecast_UsesRecurringOnlyFallbackWhenHistoryIsSparse()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var checking = TestData.AddAccount(dbContext, user.Id, "Checking", 500m);

        dbContext.Transactions.Add(new Transaction
        {
            UserId = user.Id,
            AccountId = checking.Id,
            Type = TransactionType.Expense,
            Amount = 75m,
            DateUtc = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc),
            CreatedByUserId = user.Id,
            UpdatedByUserId = user.Id
        });

        dbContext.RecurringTransactionRules.Add(new RecurringTransactionRule
        {
            UserId = user.Id,
            Title = "Bonus",
            Type = TransactionType.Income,
            Amount = 100m,
            AccountId = checking.Id,
            Frequency = RecurringFrequency.Monthly,
            StartDateUtc = new DateTime(2026, 3, 26, 0, 0, 0, DateTimeKind.Utc),
            NextRunDateUtc = new DateTime(2026, 3, 26, 0, 0, 0, DateTimeKind.Utc),
            Status = RecurringRuleStatus.Active
        });

        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, new DateTimeOffset(2026, 3, 21, 8, 0, 0, TimeSpan.Zero));
        var summary = await service.GetMonthSummaryAsync(user.Id, new ForecastQuery(null, null), CancellationToken.None);

        Assert.True(summary.HasSparseData);
        Assert.Equal(0m, summary.AverageDailyNet);
        Assert.Equal(600m, summary.ProjectedEndOfMonthBalance);
        Assert.Contains(summary.Notes, note => note.Contains("Recent transaction history is limited", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MonthForecast_IncludesSharedViewerAccountHistory()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var owner = TestData.AddUser(dbContext, "owner@example.com");
        var viewer = TestData.AddUser(dbContext, "viewer@example.com");
        var checking = TestData.AddAccount(dbContext, owner.Id, "Shared Checking", 1500m);

        dbContext.AccountMemberships.Add(new AccountMembership
        {
            AccountId = checking.Id,
            UserId = viewer.Id,
            Role = AccountMemberRole.Viewer,
            InvitedByUserId = owner.Id,
            LastModifiedByUserId = owner.Id
        });

        dbContext.Transactions.AddRange(
            CreateTransaction(owner.Id, checking.Id, TransactionType.Income, 900m, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(owner.Id, checking.Id, TransactionType.Income, 900m, new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(owner.Id, checking.Id, TransactionType.Expense, 300m, new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(owner.Id, checking.Id, TransactionType.Expense, 300m, new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(owner.Id, checking.Id, TransactionType.Expense, 150m, new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc)));

        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, new DateTimeOffset(2026, 3, 21, 8, 0, 0, TimeSpan.Zero));
        var summary = await service.GetMonthSummaryAsync(viewer.Id, new ForecastQuery(null, checking.Id), CancellationToken.None);

        Assert.Equal(1500m, summary.CurrentBalance);
        Assert.False(summary.HasSparseData);
        Assert.Equal(ForecastRiskLevel.Low, summary.RiskLevel);
    }

    [Fact]
    public async Task MonthForecast_RespectsMineScopeWhenUserAlsoHasSharedAccounts()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var owner = TestData.AddUser(dbContext, "owner@example.com");
        var viewer = TestData.AddUser(dbContext, "viewer@example.com");
        var ownAccount = TestData.AddAccount(dbContext, viewer.Id, "Personal Checking", 800m);
        var sharedAccount = TestData.AddAccount(dbContext, owner.Id, "Shared Checking", 1500m);

        dbContext.AccountMemberships.Add(new AccountMembership
        {
            AccountId = sharedAccount.Id,
            UserId = viewer.Id,
            Role = AccountMemberRole.Viewer,
            InvitedByUserId = owner.Id,
            LastModifiedByUserId = owner.Id
        });

        dbContext.Transactions.AddRange(
            CreateTransaction(viewer.Id, ownAccount.Id, TransactionType.Income, 600m, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(viewer.Id, ownAccount.Id, TransactionType.Expense, 300m, new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(viewer.Id, ownAccount.Id, TransactionType.Income, 600m, new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(viewer.Id, ownAccount.Id, TransactionType.Expense, 300m, new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(viewer.Id, ownAccount.Id, TransactionType.Income, 600m, new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(viewer.Id, ownAccount.Id, TransactionType.Expense, 150m, new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(owner.Id, sharedAccount.Id, TransactionType.Income, 900m, new DateTime(2026, 2, 12, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(owner.Id, sharedAccount.Id, TransactionType.Expense, 150m, new DateTime(2026, 2, 24, 0, 0, 0, DateTimeKind.Utc)));

        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, new DateTimeOffset(2026, 3, 21, 8, 0, 0, TimeSpan.Zero));
        var summary = await service.GetMonthSummaryAsync(viewer.Id, new ForecastQuery([ownAccount.Id], null), CancellationToken.None);

        Assert.Equal(800m, summary.CurrentBalance);
        Assert.Equal(11.67m, summary.AverageDailyNet);
    }

    private static ForecastService CreateService(FinanceTracker.Infrastructure.Persistence.ApplicationDbContext dbContext, DateTimeOffset utcNow)
        => new(dbContext, new StaticTimeProvider(utcNow), new AccountAccessService(dbContext));

    private static Transaction CreateTransaction(Guid userId, Guid accountId, TransactionType type, decimal amount, DateTime dateUtc, Guid? transferAccountId = null)
        => new()
        {
            UserId = userId,
            AccountId = accountId,
            TransferAccountId = transferAccountId,
            Type = type,
            Amount = amount,
            DateUtc = dateUtc,
            CreatedByUserId = userId,
            UpdatedByUserId = userId
        };

    private sealed class StaticTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}


