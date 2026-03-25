using System.Text;
using FinanceTracker.Application.Budgets.DTOs;
using FinanceTracker.Application.Reports.DTOs;
using FinanceTracker.Application.Transactions.DTOs;
using FinanceTracker.Backend.Tests.TestSupport;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Financial;
using FinanceTracker.Infrastructure.Reporting;

namespace FinanceTracker.Backend.Tests;

public sealed class ReportAndExportServiceTests
{
    [Fact]
    public async Task ReportsOverview_ExcludesTransfersFromIncomeAndExpenseTotals()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var checking = TestData.AddAccount(dbContext, user.Id, "Checking", 1000m);
        var savings = TestData.AddAccount(dbContext, user.Id, "Savings", 500m, AccountType.SavingsAccount);
        var salaryCategory = TestData.AddCategory(dbContext, user.Id, "Salary", CategoryType.Income);
        var foodCategory = TestData.AddCategory(dbContext, user.Id, "Food", CategoryType.Expense);
        dbContext.Transactions.AddRange(
            CreateTransaction(user.Id, checking.Id, TransactionType.Income, 2000m, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), salaryCategory.Id),
            CreateTransaction(user.Id, checking.Id, TransactionType.Expense, 250m, new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc), foodCategory.Id),
            CreateTransaction(user.Id, checking.Id, TransactionType.Transfer, 300m, new DateTime(2026, 3, 7, 0, 0, 0, DateTimeKind.Utc), null, null, null, savings.Id));
        await dbContext.SaveChangesAsync();

        var service = CreateReportService(dbContext);
        var overview = await service.GetOverviewAsync(user.Id, new ReportQuery(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc), null, null), CancellationToken.None);

        Assert.Equal(2000m, overview.Summary.TotalIncome);
        Assert.Equal(250m, overview.Summary.TotalExpense);
        Assert.Single(overview.CategorySpend);
    }

    [Fact]
    public async Task ReportsOverview_IncludesPreviousPeriodComparisonAndTopMerchants()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var checking = TestData.AddAccount(dbContext, user.Id, "Checking", 500m);
        var salaryCategory = TestData.AddCategory(dbContext, user.Id, "Salary", CategoryType.Income);
        var foodCategory = TestData.AddCategory(dbContext, user.Id, "Food", CategoryType.Expense);
        var rentCategory = TestData.AddCategory(dbContext, user.Id, "Rent", CategoryType.Expense);

        dbContext.Transactions.AddRange(
            CreateTransaction(user.Id, checking.Id, TransactionType.Income, 1500m, new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc), salaryCategory.Id, "Employer"),
            CreateTransaction(user.Id, checking.Id, TransactionType.Expense, 600m, new DateTime(2026, 2, 12, 0, 0, 0, DateTimeKind.Utc), rentCategory.Id, "Landlord"),
            CreateTransaction(user.Id, checking.Id, TransactionType.Income, 2000m, new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc), salaryCategory.Id, "Employer"),
            CreateTransaction(user.Id, checking.Id, TransactionType.Expense, 850m, new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc), rentCategory.Id, "Landlord"),
            CreateTransaction(user.Id, checking.Id, TransactionType.Expense, 300m, new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), foodCategory.Id, "Fresh Mart"),
            CreateTransaction(user.Id, checking.Id, TransactionType.Expense, 120m, new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc), foodCategory.Id, "Fresh Mart"));
        await dbContext.SaveChangesAsync();

        var service = CreateReportService(dbContext);
        var overview = await service.GetOverviewAsync(user.Id, new ReportQuery(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc), null, null), CancellationToken.None);

        Assert.Equal(1500m, overview.Comparison.PreviousTotalIncome);
        Assert.Equal(600m, overview.Comparison.PreviousTotalExpense);
        Assert.Equal(900m, overview.Comparison.PreviousNetCashFlow);
        Assert.Equal("Landlord", overview.TopMerchants.First().MerchantName);
        Assert.Equal(850m, overview.TopMerchants.First().Amount);
        Assert.Equal(2, overview.TopMerchants.Count);
    }

    [Fact]
    public async Task ReportsOverview_IncludesSharedViewerTransactionsForAccessibleAccount()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var owner = TestData.AddUser(dbContext, "owner@example.com");
        var viewer = TestData.AddUser(dbContext, "viewer@example.com");
        var account = TestData.AddAccount(dbContext, owner.Id, "Family Checking", 2000m);
        var incomeCategory = TestData.AddCategory(dbContext, owner.Id, "Salary", CategoryType.Income);
        var expenseCategory = TestData.AddCategory(dbContext, owner.Id, "Groceries", CategoryType.Expense);

        dbContext.AccountMemberships.Add(new AccountMembership
        {
            AccountId = account.Id,
            UserId = viewer.Id,
            Role = AccountMemberRole.Viewer,
            InvitedByUserId = owner.Id,
            LastModifiedByUserId = owner.Id
        });

        dbContext.Transactions.AddRange(
            CreateTransaction(owner.Id, account.Id, TransactionType.Income, 3000m, new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc), incomeCategory.Id, "Payroll"),
            CreateTransaction(owner.Id, account.Id, TransactionType.Expense, 400m, new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc), expenseCategory.Id, "Supermarket"));
        await dbContext.SaveChangesAsync();

        var service = CreateReportService(dbContext);
        var overview = await service.GetOverviewAsync(viewer.Id, new ReportQuery(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc), null, account.Id), CancellationToken.None);

        Assert.Equal(3000m, overview.Summary.TotalIncome);
        Assert.Equal(400m, overview.Summary.TotalExpense);
        Assert.Single(overview.CategorySpend);
    }

    [Fact]
    public async Task ReportOverviewPdfExport_ReturnsPdfBytes()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var account = TestData.AddAccount(dbContext, user.Id, "Checking", 1000m);
        var incomeCategory = TestData.AddCategory(dbContext, user.Id, "Salary", CategoryType.Income);
        dbContext.Transactions.Add(CreateTransaction(user.Id, account.Id, TransactionType.Income, 5000m, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), incomeCategory.Id, "Employer"));
        await dbContext.SaveChangesAsync();

        var exportService = new ExportService(dbContext, CreateReportService(dbContext), new BudgetService(dbContext, new AccountAccessService(dbContext)));
        var file = await exportService.ExportReportOverviewPdfAsync(user.Id, new ReportQuery(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc), null, null), CancellationToken.None);
        var header = Encoding.ASCII.GetString(file.Content.Take(8).ToArray());

        Assert.Equal("application/pdf", file.ContentType);
        Assert.EndsWith(".pdf", file.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("%PDF-1.", header, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransactionExportCsv_RespectsTypeAndSearchFilters()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var account = TestData.AddAccount(dbContext, user.Id, "Checking", 1000m);
        var incomeCategory = TestData.AddCategory(dbContext, user.Id, "Salary", CategoryType.Income);
        var expenseCategory = TestData.AddCategory(dbContext, user.Id, "Food", CategoryType.Expense);
        dbContext.Transactions.AddRange(
            CreateTransaction(user.Id, account.Id, TransactionType.Income, 5000m, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), incomeCategory.Id, "Employer"),
            CreateTransaction(user.Id, account.Id, TransactionType.Expense, 150m, new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc), expenseCategory.Id, "Grocery World", "Weekly shopping"));
        await dbContext.SaveChangesAsync();

        var exportService = new ExportService(dbContext, CreateReportService(dbContext), new BudgetService(dbContext, new AccountAccessService(dbContext)));
        var file = await exportService.ExportTransactionsCsvAsync(user.Id, new TransactionListQuery { Type = TransactionType.Expense, Search = "Grocery" }, CancellationToken.None);
        var csv = Encoding.UTF8.GetString(file.Content);

        Assert.Contains("Grocery World", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Employer", csv, StringComparison.Ordinal);
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


