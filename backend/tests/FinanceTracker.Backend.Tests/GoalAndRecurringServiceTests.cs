using FinanceTracker.Application.Goals.DTOs;
using FinanceTracker.Application.RecurringTransactions.DTOs;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Financial;
using FinanceTracker.Backend.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

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

        var service = new GoalService(dbContext);
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
    public async Task ProcessDue_IsIdempotentForTheSameOccurrence()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var user = TestData.AddUser(dbContext);
        var account = TestData.AddAccount(dbContext, user.Id, "Checking", 1000m);
        var category = TestData.AddCategory(dbContext, user.Id, "Utilities", CategoryType.Expense);
        await dbContext.SaveChangesAsync();

        var recurringService = new RecurringTransactionService(dbContext, new TransactionService(dbContext, new CategorySeeder(dbContext)));
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
}
