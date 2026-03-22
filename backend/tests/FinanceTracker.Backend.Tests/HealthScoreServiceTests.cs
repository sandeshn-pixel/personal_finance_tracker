using FinanceTracker.Backend.Tests.TestSupport;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Insights;

namespace FinanceTracker.Backend.Tests;

public sealed class HealthScoreServiceTests
{
    [Fact]
    public async Task HealthScore_ReturnsStrongScoreForConsistentHealthyHistory()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var account = TestData.AddAccount(dbContext, user.Id, "Checking", 6000m);
        var incomeCategory = TestData.AddCategory(dbContext, user.Id, "Salary", CategoryType.Income);
        var expenseCategory = TestData.AddCategory(dbContext, user.Id, "Living", CategoryType.Expense);

        dbContext.Transactions.AddRange(
            CreateTransaction(user.Id, account.Id, incomeCategory.Id, TransactionType.Income, 3000m, new DateTime(2025, 12, 5, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, account.Id, expenseCategory.Id, TransactionType.Expense, 1800m, new DateTime(2025, 12, 18, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, account.Id, incomeCategory.Id, TransactionType.Income, 3000m, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, account.Id, expenseCategory.Id, TransactionType.Expense, 1800m, new DateTime(2026, 1, 18, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, account.Id, incomeCategory.Id, TransactionType.Income, 3000m, new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, account.Id, expenseCategory.Id, TransactionType.Expense, 1800m, new DateTime(2026, 2, 18, 0, 0, 0, DateTimeKind.Utc)));

        dbContext.Budgets.AddRange(
            CreateBudget(user.Id, expenseCategory.Id, 2025, 12, 2000m),
            CreateBudget(user.Id, expenseCategory.Id, 2026, 1, 2000m),
            CreateBudget(user.Id, expenseCategory.Id, 2026, 2, 2000m));

        await dbContext.SaveChangesAsync();

        var service = new HealthScoreService(dbContext, new StaticTimeProvider(new DateTimeOffset(2026, 3, 21, 8, 0, 0, TimeSpan.Zero)));
        var result = await service.GetAsync(user.Id, CancellationToken.None);

        Assert.Equal(100, result.Score);
        Assert.Equal("Strong", result.Band.ToString());
        Assert.False(result.HasSparseData);
        Assert.Equal(4, result.Factors.Count);
    }

    [Fact]
    public async Task HealthScore_UsesNeutralBudgetFallbackWhenBudgetsAreMissing()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var account = TestData.AddAccount(dbContext, user.Id, "Checking", 1500m);
        var incomeCategory = TestData.AddCategory(dbContext, user.Id, "Salary", CategoryType.Income);
        var expenseCategory = TestData.AddCategory(dbContext, user.Id, "Food", CategoryType.Expense);

        dbContext.Transactions.AddRange(
            CreateTransaction(user.Id, account.Id, incomeCategory.Id, TransactionType.Income, 2000m, new DateTime(2025, 12, 5, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, account.Id, expenseCategory.Id, TransactionType.Expense, 1400m, new DateTime(2025, 12, 19, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, account.Id, incomeCategory.Id, TransactionType.Income, 2000m, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, account.Id, expenseCategory.Id, TransactionType.Expense, 1500m, new DateTime(2026, 1, 19, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, account.Id, incomeCategory.Id, TransactionType.Income, 2000m, new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, account.Id, expenseCategory.Id, TransactionType.Expense, 1450m, new DateTime(2026, 2, 19, 0, 0, 0, DateTimeKind.Utc)));

        await dbContext.SaveChangesAsync();

        var service = new HealthScoreService(dbContext, new StaticTimeProvider(new DateTimeOffset(2026, 3, 21, 8, 0, 0, TimeSpan.Zero)));
        var result = await service.GetAsync(user.Id, CancellationToken.None);
        var budgetFactor = result.Factors.Single(x => x.Key == "budget-adherence");

        Assert.True(budgetFactor.IsFallback);
        Assert.Equal(60, budgetFactor.Score);
        Assert.True(result.HasSparseData);
    }

    [Fact]
    public async Task HealthScore_ExcludesTransfersFromSavingsRateComputation()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var checking = TestData.AddAccount(dbContext, user.Id, "Checking", 1200m);
        var savings = TestData.AddAccount(dbContext, user.Id, "Savings", 800m, AccountType.SavingsAccount);
        var incomeCategory = TestData.AddCategory(dbContext, user.Id, "Salary", CategoryType.Income);
        var expenseCategory = TestData.AddCategory(dbContext, user.Id, "Bills", CategoryType.Expense);

        dbContext.Transactions.AddRange(
            CreateTransaction(user.Id, checking.Id, incomeCategory.Id, TransactionType.Income, 1000m, new DateTime(2025, 12, 3, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, checking.Id, expenseCategory.Id, TransactionType.Expense, 800m, new DateTime(2025, 12, 9, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, checking.Id, incomeCategory.Id, TransactionType.Income, 1000m, new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, checking.Id, expenseCategory.Id, TransactionType.Expense, 800m, new DateTime(2026, 1, 9, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, checking.Id, incomeCategory.Id, TransactionType.Income, 1000m, new DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(user.Id, checking.Id, expenseCategory.Id, TransactionType.Expense, 800m, new DateTime(2026, 2, 9, 0, 0, 0, DateTimeKind.Utc)),
            new Transaction { UserId = user.Id, AccountId = checking.Id, TransferAccountId = savings.Id, Type = TransactionType.Transfer, Amount = 700m, DateUtc = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc) });

        await dbContext.SaveChangesAsync();

        var service = new HealthScoreService(dbContext, new StaticTimeProvider(new DateTimeOffset(2026, 3, 21, 8, 0, 0, TimeSpan.Zero)));
        var result = await service.GetAsync(user.Id, CancellationToken.None);
        var savingsFactor = result.Factors.Single(x => x.Key == "savings-rate");

        Assert.Equal(20m, savingsFactor.MetricValue);
        Assert.Equal(100, savingsFactor.Score);
    }

    private static Transaction CreateTransaction(Guid userId, Guid accountId, Guid categoryId, TransactionType type, decimal amount, DateTime dateUtc)
        => new()
        {
            UserId = userId,
            AccountId = accountId,
            CategoryId = categoryId,
            Type = type,
            Amount = amount,
            DateUtc = dateUtc
        };

    private static Budget CreateBudget(Guid userId, Guid categoryId, int year, int month, decimal amount)
        => new()
        {
            UserId = userId,
            CategoryId = categoryId,
            Year = year,
            Month = month,
            Amount = amount,
            AlertThresholdPercent = 80
        };

    private sealed class StaticTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
