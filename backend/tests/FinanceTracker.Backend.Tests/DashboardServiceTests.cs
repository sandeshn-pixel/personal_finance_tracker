using FinanceTracker.Application.Dashboard.DTOs;
using FinanceTracker.Backend.Tests.TestSupport;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Financial;

namespace FinanceTracker.Backend.Tests;

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task DashboardSummary_IncludesSharedViewerBudgetVisibilityAsReadOnly()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var owner = TestData.AddUser(dbContext, "owner@example.com");
        owner.FirstName = "Owner";
        owner.LastName = "User";
        var viewer = TestData.AddUser(dbContext, "viewer@example.com");
        var account = TestData.AddAccount(dbContext, owner.Id, "Family Checking", 2200m);
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
            new Transaction
            {
                UserId = owner.Id,
                AccountId = account.Id,
                CategoryId = incomeCategory.Id,
                Type = TransactionType.Income,
                Amount = 4000m,
                DateUtc = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc),
                Merchant = "Payroll",
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id
            },
            new Transaction
            {
                UserId = owner.Id,
                AccountId = account.Id,
                CategoryId = expenseCategory.Id,
                Type = TransactionType.Expense,
                Amount = 500m,
                DateUtc = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc),
                Merchant = "Store",
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id
            });

        dbContext.Budgets.Add(new Budget
        {
            UserId = owner.Id,
            CategoryId = expenseCategory.Id,
            Year = 2026,
            Month = 3,
            Amount = 1000m,
            AlertThresholdPercent = 80
        });

        await dbContext.SaveChangesAsync();

        var service = new DashboardService(dbContext, new AccountAccessService(dbContext));
        var summary = await service.GetSummaryAsync(viewer.Id, new DashboardQuery(null, null), CancellationToken.None);

        Assert.Equal(4000m, summary.CurrentMonthIncome);
        Assert.Equal(500m, summary.CurrentMonthExpense);
        Assert.Equal(2200m, summary.NetBalance);
        Assert.Equal(2, summary.RecentTransactions.Count);
        Assert.Single(summary.AccountBalanceDistribution);
        var visibleBudget = Assert.Single(summary.BudgetUsage);
        Assert.False(visibleBudget.CanManage);
        Assert.Equal("Owner User", visibleBudget.OwnerDisplayName);
        Assert.Equal(1000m, summary.BudgetHealth.TotalBudgeted);
        Assert.Equal(500m, summary.BudgetHealth.TotalSpent);
        Assert.Equal(1, summary.BudgetHealth.SharedReadOnlyBudgetCount);
        Assert.Equal(1, summary.BudgetHealth.SharedOwnerCount);
        Assert.Equal(0, summary.SavingsAutomation.ActiveGoalsCount);
        Assert.Equal(0, summary.SavingsAutomation.ActiveRecurringRulesCount);
    }

    [Fact]
    public async Task DashboardSummary_RespectsOwnedAndSharedScopeSelections()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var owner = TestData.AddUser(dbContext, "owner@example.com");
        owner.FirstName = "Shared";
        owner.LastName = "Owner";
        var viewer = TestData.AddUser(dbContext, "viewer@example.com");
        viewer.FirstName = "Viewer";
        viewer.LastName = "User";

        var ownAccount = TestData.AddAccount(dbContext, viewer.Id, "Personal Checking", 900m);
        var sharedAccount = TestData.AddAccount(dbContext, owner.Id, "Family Checking", 2200m);
        var viewerIncomeCategory = TestData.AddCategory(dbContext, viewer.Id, "Salary", CategoryType.Income);
        var viewerExpenseCategory = TestData.AddCategory(dbContext, viewer.Id, "Food", CategoryType.Expense);
        var ownerIncomeCategory = TestData.AddCategory(dbContext, owner.Id, "Payroll", CategoryType.Income);
        var ownerExpenseCategory = TestData.AddCategory(dbContext, owner.Id, "Groceries", CategoryType.Expense);

        dbContext.AccountMemberships.Add(new AccountMembership
        {
            AccountId = sharedAccount.Id,
            UserId = viewer.Id,
            Role = AccountMemberRole.Viewer,
            InvitedByUserId = owner.Id,
            LastModifiedByUserId = owner.Id
        });

        dbContext.Transactions.AddRange(
            new Transaction
            {
                UserId = viewer.Id,
                AccountId = ownAccount.Id,
                CategoryId = viewerIncomeCategory.Id,
                Type = TransactionType.Income,
                Amount = 500m,
                DateUtc = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc),
                CreatedByUserId = viewer.Id,
                UpdatedByUserId = viewer.Id
            },
            new Transaction
            {
                UserId = viewer.Id,
                AccountId = ownAccount.Id,
                CategoryId = viewerExpenseCategory.Id,
                Type = TransactionType.Expense,
                Amount = 100m,
                DateUtc = new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc),
                CreatedByUserId = viewer.Id,
                UpdatedByUserId = viewer.Id
            },
            new Transaction
            {
                UserId = owner.Id,
                AccountId = sharedAccount.Id,
                CategoryId = ownerIncomeCategory.Id,
                Type = TransactionType.Income,
                Amount = 4000m,
                DateUtc = new DateTime(2026, 3, 3, 0, 0, 0, DateTimeKind.Utc),
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id
            },
            new Transaction
            {
                UserId = owner.Id,
                AccountId = sharedAccount.Id,
                CategoryId = ownerExpenseCategory.Id,
                Type = TransactionType.Expense,
                Amount = 500m,
                DateUtc = new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc),
                CreatedByUserId = owner.Id,
                UpdatedByUserId = owner.Id
            });

        dbContext.Budgets.AddRange(
            new Budget
            {
                UserId = viewer.Id,
                CategoryId = viewerExpenseCategory.Id,
                Year = 2026,
                Month = 3,
                Amount = 300m,
                AlertThresholdPercent = 80
            },
            new Budget
            {
                UserId = owner.Id,
                CategoryId = ownerExpenseCategory.Id,
                Year = 2026,
                Month = 3,
                Amount = 800m,
                AlertThresholdPercent = 80
            });

        dbContext.Goals.Add(new Goal
        {
            UserId = viewer.Id,
            Name = "Emergency Fund",
            TargetAmount = 2000m,
            CurrentAmount = 700m,
            Status = GoalStatus.Active
        });

        await dbContext.SaveChangesAsync();

        var service = new DashboardService(dbContext, new AccountAccessService(dbContext));

        var mineSummary = await service.GetSummaryAsync(viewer.Id, new DashboardQuery([ownAccount.Id], null), CancellationToken.None);
        Assert.Equal(900m, mineSummary.NetBalance);
        Assert.Equal(500m, mineSummary.CurrentMonthIncome);
        Assert.Equal(100m, mineSummary.CurrentMonthExpense);
        Assert.Equal(300m, mineSummary.BudgetHealth.TotalBudgeted);
        Assert.Equal(0, mineSummary.BudgetHealth.SharedReadOnlyBudgetCount);
        Assert.Single(mineSummary.GoalProgress);
        Assert.Equal(1, mineSummary.SavingsAutomation.ActiveGoalsCount);

        var sharedSummary = await service.GetSummaryAsync(viewer.Id, new DashboardQuery([sharedAccount.Id], null), CancellationToken.None);
        Assert.Equal(2200m, sharedSummary.NetBalance);
        Assert.Equal(4000m, sharedSummary.CurrentMonthIncome);
        Assert.Equal(500m, sharedSummary.CurrentMonthExpense);
        Assert.Equal(800m, sharedSummary.BudgetHealth.TotalBudgeted);
        Assert.Equal(1, sharedSummary.BudgetHealth.SharedReadOnlyBudgetCount);
        Assert.Empty(sharedSummary.GoalProgress);
        Assert.Equal(0, sharedSummary.SavingsAutomation.ActiveGoalsCount);
    }
}
