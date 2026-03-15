using System.Text;
using FinanceTracker.Application.Budgets.DTOs;
using FinanceTracker.Application.Reports.DTOs;
using FinanceTracker.Application.Transactions.DTOs;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Financial;
using FinanceTracker.Infrastructure.Reporting;
using FinanceTracker.Backend.Tests.TestSupport;

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
            new FinanceTracker.Domain.Entities.Transaction { UserId = user.Id, AccountId = checking.Id, Type = TransactionType.Income, Amount = 2000m, DateUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CategoryId = salaryCategory.Id },
            new FinanceTracker.Domain.Entities.Transaction { UserId = user.Id, AccountId = checking.Id, Type = TransactionType.Expense, Amount = 250m, DateUtc = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc), CategoryId = foodCategory.Id },
            new FinanceTracker.Domain.Entities.Transaction { UserId = user.Id, AccountId = checking.Id, TransferAccountId = savings.Id, Type = TransactionType.Transfer, Amount = 300m, DateUtc = new DateTime(2026, 3, 7, 0, 0, 0, DateTimeKind.Utc) });
        await dbContext.SaveChangesAsync();

        var service = new ReportService(dbContext);
        var overview = await service.GetOverviewAsync(user.Id, new ReportQuery(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc), null), CancellationToken.None);

        Assert.Equal(2000m, overview.Summary.TotalIncome);
        Assert.Equal(250m, overview.Summary.TotalExpense);
        Assert.Single(overview.CategorySpend);
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
            new FinanceTracker.Domain.Entities.Transaction { UserId = user.Id, AccountId = account.Id, Type = TransactionType.Income, Amount = 5000m, DateUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), CategoryId = incomeCategory.Id, Merchant = "Employer" },
            new FinanceTracker.Domain.Entities.Transaction { UserId = user.Id, AccountId = account.Id, Type = TransactionType.Expense, Amount = 150m, DateUtc = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc), CategoryId = expenseCategory.Id, Merchant = "Grocery World", Note = "Weekly shopping" });
        await dbContext.SaveChangesAsync();

        var exportService = new ExportService(dbContext, new ReportService(dbContext), new BudgetService(dbContext));
        var file = await exportService.ExportTransactionsCsvAsync(user.Id, new TransactionListQuery { Type = TransactionType.Expense, Search = "Grocery" }, CancellationToken.None);
        var csv = Encoding.UTF8.GetString(file.Content);

        Assert.Contains("Grocery World", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("Employer", csv, StringComparison.Ordinal);
    }
}
