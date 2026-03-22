using FinanceTracker.Application.Forecasting.DTOs;
using FinanceTracker.Backend.Tests.TestSupport;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
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
            new Transaction { UserId = user.Id, AccountId = checking.Id, Type = TransactionType.Income, Amount = 900m, DateUtc = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
            new Transaction { UserId = user.Id, AccountId = checking.Id, Type = TransactionType.Income, Amount = 900m, DateUtc = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc) },
            new Transaction { UserId = user.Id, AccountId = checking.Id, Type = TransactionType.Expense, Amount = 300m, DateUtc = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc) },
            new Transaction { UserId = user.Id, AccountId = checking.Id, Type = TransactionType.Expense, Amount = 300m, DateUtc = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc) },
            new Transaction { UserId = user.Id, AccountId = checking.Id, Type = TransactionType.Expense, Amount = 300m, DateUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Transaction { UserId = user.Id, AccountId = checking.Id, Type = TransactionType.Expense, Amount = 300m, DateUtc = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc) },
            new Transaction { UserId = user.Id, AccountId = checking.Id, TransferAccountId = savings.Id, Type = TransactionType.Transfer, Amount = 700m, DateUtc = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc) });

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

        var service = new ForecastService(dbContext, new StaticTimeProvider(new DateTimeOffset(2026, 3, 21, 8, 0, 0, TimeSpan.Zero)));
        var summary = await service.GetMonthSummaryAsync(user.Id, new ForecastQuery(null), CancellationToken.None);

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
            RecurringTransactionId = ruleId
        });

        await dbContext.SaveChangesAsync();

        var service = new ForecastService(dbContext, new StaticTimeProvider(new DateTimeOffset(2026, 3, 21, 8, 0, 0, TimeSpan.Zero)));
        var summary = await service.GetMonthSummaryAsync(user.Id, new ForecastQuery(null), CancellationToken.None);

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
            DateUtc = new DateTime(2026, 3, 18, 0, 0, 0, DateTimeKind.Utc)
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

        var service = new ForecastService(dbContext, new StaticTimeProvider(new DateTimeOffset(2026, 3, 21, 8, 0, 0, TimeSpan.Zero)));
        var summary = await service.GetMonthSummaryAsync(user.Id, new ForecastQuery(null), CancellationToken.None);

        Assert.True(summary.HasSparseData);
        Assert.Equal(0m, summary.AverageDailyNet);
        Assert.Equal(600m, summary.ProjectedEndOfMonthBalance);
        Assert.Contains(summary.Notes, note => note.Contains("Recent transaction history is limited", StringComparison.Ordinal));
    }

    private sealed class StaticTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
