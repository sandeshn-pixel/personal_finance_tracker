using FinanceTracker.Application.Insights.DTOs;
using FinanceTracker.Application.Reports.DTOs;
using FinanceTracker.Backend.Tests.TestSupport;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Financial;
using FinanceTracker.Infrastructure.Insights;
using FinanceTracker.Infrastructure.Reporting;

namespace FinanceTracker.Backend.Tests;

public sealed class AdvancedReportingInsightsTests
{
    [Fact]
    public async Task ReportTrends_ExcludeTransfers_FromIncomeExpenseAndSavingsRate()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var checking = TestData.AddAccount(dbContext, user.Id, "Checking", 1000m);
        var savings = TestData.AddAccount(dbContext, user.Id, "Savings", 500m, AccountType.SavingsAccount);
        var incomeCategory = TestData.AddCategory(dbContext, user.Id, "Salary", CategoryType.Income);
        var expenseCategory = TestData.AddCategory(dbContext, user.Id, "Food", CategoryType.Expense);

        dbContext.Transactions.AddRange(
            CreateTransaction(user.Id, checking.Id, TransactionType.Income, 3000m, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), incomeCategory.Id),
            CreateTransaction(user.Id, checking.Id, TransactionType.Expense, 1200m, new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc), expenseCategory.Id),
            CreateTransaction(user.Id, checking.Id, TransactionType.Transfer, 400m, new DateTime(2026, 3, 3, 0, 0, 0, DateTimeKind.Utc), null, transferAccountId: savings.Id));
        await dbContext.SaveChangesAsync();

        var reportService = CreateReportService(dbContext);
        var trends = await reportService.GetTrendsAsync(user.Id, new ReportTrendsQuery(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc), ReportTimeBucket.Month), CancellationToken.None);

        Assert.Equal(3000m, trends.IncomeExpenseTrend.Sum(x => x.Income));
        Assert.Equal(1200m, trends.IncomeExpenseTrend.Sum(x => x.Expense));
        var savingsRatePoint = Assert.Single(trends.SavingsRateTrend, x => x.Income > 0m);
        Assert.Equal(60m, savingsRatePoint.SavingsRatePercent);
    }

    [Fact]
    public async Task NetWorthReport_UsesStoredCreditCardSignToReduceNetWorth()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var checkingAccount = TestData.AddAccount(dbContext, user.Id, "Checking", 1000m, AccountType.BankAccount);
        checkingAccount.CurrentBalance = 1000m;
        var creditCardAccount = TestData.AddAccount(dbContext, user.Id, "Credit Card", -250m, AccountType.CreditCard);
        creditCardAccount.CurrentBalance = -250m;
        await dbContext.SaveChangesAsync();

        var reportService = CreateReportService(dbContext);
        var netWorth = await reportService.GetNetWorthAsync(user.Id, new ReportNetWorthQuery(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc)), CancellationToken.None);

        Assert.Equal(750m, netWorth.CurrentNetWorth);
        Assert.Equal(750m, netWorth.StartingNetWorth);
        Assert.Equal(1, netWorth.IncludedLiabilityAccountCount);
        Assert.Equal(-250m, netWorth.Points.Last().LiabilityBalance);
    }

    [Fact]
    public async Task Insights_GenerateDeterministicCategoryAndSavingsObservations()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var checking = TestData.AddAccount(dbContext, user.Id, "Checking", 1000m);
        var incomeCategory = TestData.AddCategory(dbContext, user.Id, "Salary", CategoryType.Income);
        var foodCategory = TestData.AddCategory(dbContext, user.Id, "Food", CategoryType.Expense);

        dbContext.Transactions.AddRange(
            CreateTransaction(user.Id, checking.Id, TransactionType.Income, 3000m, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), incomeCategory.Id, "Employer"),
            CreateTransaction(user.Id, checking.Id, TransactionType.Expense, 900m, new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc), foodCategory.Id, "Grocer"),
            CreateTransaction(user.Id, checking.Id, TransactionType.Expense, 1500m, new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc), foodCategory.Id, "Dining"),
            CreateTransaction(user.Id, checking.Id, TransactionType.Income, 3000m, new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc), incomeCategory.Id, "Employer"),
            CreateTransaction(user.Id, checking.Id, TransactionType.Expense, 1800m, new DateTime(2026, 2, 12, 0, 0, 0, DateTimeKind.Utc), foodCategory.Id, "Dining"));
        await dbContext.SaveChangesAsync();

        var reportService = CreateReportService(dbContext);
        var service = new InsightsService(dbContext, new AccountAccessService(dbContext), reportService);
        var insights = await service.GetAsync(user.Id, new InsightsQuery(new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc)), CancellationToken.None);

        Assert.Contains(insights.Items, item => item.Key == "category-spend-change");
        Assert.Contains(insights.Items, item => item.Key == "savings-rate-change");
    }

    private static ReportService CreateReportService(FinanceTracker.Infrastructure.Persistence.ApplicationDbContext dbContext)
        => new(dbContext, new AccountAccessService(dbContext));

    private static Transaction CreateTransaction(
        Guid userId,
        Guid accountId,
        TransactionType type,
        decimal amount,
        DateTime dateUtc,
        Guid? categoryId = null,
        string? merchant = null,
        string? note = null,
        Guid? transferAccountId = null)
        => new()
        {
            UserId = userId,
            AccountId = accountId,
            TransferAccountId = transferAccountId,
            Type = type,
            Amount = amount,
            DateUtc = dateUtc,
            CategoryId = categoryId,
            Merchant = merchant,
            Note = note,
            CreatedByUserId = userId,
            UpdatedByUserId = userId
        };
}

