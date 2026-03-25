using FinanceTracker.Application.Budgets.DTOs;
using FinanceTracker.Application.Transactions.DTOs;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Financial;
using FinanceTracker.Backend.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Backend.Tests;

public sealed class TransactionAndBudgetServiceTests
{
    [Fact]
    public async Task CreateExpenseAndDelete_RestoresAccountBalance()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var account = TestData.AddAccount(dbContext, user.Id, "Checking", 1000m);
        var category = TestData.AddCategory(dbContext, user.Id, "Food", CategoryType.Expense);
        await dbContext.SaveChangesAsync();

        var service = new TransactionService(dbContext, new CategorySeeder(dbContext), new AccountAccessService(dbContext));
        var created = await service.CreateAsync(user.Id, new UpsertTransactionRequest
        {
            AccountId = account.Id,
            Type = TransactionType.Expense,
            Amount = 125.50m,
            DateUtc = DateTime.UtcNow,
            CategoryId = category.Id,
            Tags = []
        }, CancellationToken.None);

        Assert.Equal(874.50m, (await dbContext.Accounts.SingleAsync(x => x.Id == account.Id)).CurrentBalance);

        await service.DeleteAsync(user.Id, created.Id, CancellationToken.None);

        Assert.Equal(1000m, (await dbContext.Accounts.SingleAsync(x => x.Id == account.Id)).CurrentBalance);
    }

    [Fact]
    public async Task Transfer_MovesMoneyBetweenAccountsSafely()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var source = TestData.AddAccount(dbContext, user.Id, "Checking", 1000m);
        var destination = TestData.AddAccount(dbContext, user.Id, "Savings", 200m, AccountType.SavingsAccount);
        await dbContext.SaveChangesAsync();

        var service = new TransactionService(dbContext, new CategorySeeder(dbContext), new AccountAccessService(dbContext));
        await service.CreateAsync(user.Id, new UpsertTransactionRequest
        {
            AccountId = source.Id,
            TransferAccountId = destination.Id,
            Type = TransactionType.Transfer,
            Amount = 150m,
            DateUtc = DateTime.UtcNow,
            Tags = []
        }, CancellationToken.None);

        Assert.Equal(850m, (await dbContext.Accounts.SingleAsync(x => x.Id == source.Id)).CurrentBalance);
        Assert.Equal(350m, (await dbContext.Accounts.SingleAsync(x => x.Id == destination.Id)).CurrentBalance);
    }

    [Fact]
    public async Task BudgetSummary_UsesOnlyExpenseTransactions()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var account = TestData.AddAccount(dbContext, user.Id, "Checking", 5000m);
        var expenseCategory = TestData.AddCategory(dbContext, user.Id, "Food", CategoryType.Expense);
        var incomeCategory = TestData.AddCategory(dbContext, user.Id, "Salary", CategoryType.Income);
        dbContext.Budgets.Add(new Budget
        {
            UserId = user.Id,
            CategoryId = expenseCategory.Id,
            Year = 2026,
            Month = 3,
            Amount = 500m,
            AlertThresholdPercent = 80
        });
        dbContext.Transactions.AddRange(
            new Transaction
            {
                UserId = user.Id,
                AccountId = account.Id,
                Type = TransactionType.Expense,
                Amount = 120m,
                DateUtc = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc),
                CategoryId = expenseCategory.Id,
                CreatedByUserId = user.Id,
                UpdatedByUserId = user.Id
            },
            new Transaction
            {
                UserId = user.Id,
                AccountId = account.Id,
                Type = TransactionType.Income,
                Amount = 1000m,
                DateUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                CategoryId = incomeCategory.Id,
                CreatedByUserId = user.Id,
                UpdatedByUserId = user.Id
            });
        await dbContext.SaveChangesAsync();

        var service = new BudgetService(dbContext, new AccountAccessService(dbContext));
        var summary = await service.GetSummaryAsync(user.Id, new BudgetMonthQuery(2026, 3), CancellationToken.None);

        Assert.Equal(500m, summary.TotalBudgeted);
        Assert.Equal(120m, summary.TotalSpent);
        Assert.Equal(380m, summary.TotalRemaining);
    }

    [Fact]
    public async Task SharedViewer_SeesOwnerBudgetForAccessibleSharedAccountActivity_AsReadOnly()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var owner = TestData.AddUser(dbContext, "owner@example.com");
        owner.FirstName = "Owner";
        owner.LastName = "User";
        var viewer = TestData.AddUser(dbContext, "viewer@example.com");
        viewer.FirstName = "Viewer";
        viewer.LastName = "User";
        var sharedAccount = TestData.AddAccount(dbContext, owner.Id, "Family Checking", 2000m);
        var privateAccount = TestData.AddAccount(dbContext, owner.Id, "Private Checking", 1000m);
        var category = TestData.AddCategory(dbContext, owner.Id, "Groceries", CategoryType.Expense);

        dbContext.AccountMemberships.Add(new AccountMembership
        {
            AccountId = sharedAccount.Id,
            UserId = viewer.Id,
            Role = AccountMemberRole.Viewer,
            InvitedByUserId = owner.Id,
            LastModifiedByUserId = owner.Id
        });

        dbContext.Budgets.Add(new Budget
        {
            UserId = owner.Id,
            CategoryId = category.Id,
            Year = 2026,
            Month = 3,
            Amount = 600m,
            AlertThresholdPercent = 80
        });

        dbContext.Transactions.AddRange(
            new Transaction
            {
                UserId = owner.Id,
                AccountId = sharedAccount.Id,
                Type = TransactionType.Expense,
                Amount = 150m,
                DateUtc = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc),
                CategoryId = category.Id,
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id
            },
            new Transaction
            {
                UserId = owner.Id,
                AccountId = privateAccount.Id,
                Type = TransactionType.Expense,
                Amount = 90m,
                DateUtc = new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Utc),
                CategoryId = category.Id,
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id
            });
        await dbContext.SaveChangesAsync();

        var service = new BudgetService(dbContext, new AccountAccessService(dbContext));
        var budgets = await service.ListByMonthAsync(viewer.Id, new BudgetMonthQuery(2026, 3), CancellationToken.None);
        var summary = await service.GetSummaryAsync(viewer.Id, new BudgetMonthQuery(2026, 3), CancellationToken.None);

        var visible = Assert.Single(budgets);
        Assert.False(visible.CanManage);
        Assert.Equal("Owner User", visible.OwnerDisplayName);
        Assert.Equal(150m, visible.ActualSpent);
        Assert.Equal(600m, summary.TotalBudgeted);
        Assert.Equal(150m, summary.TotalSpent);
    }
}
