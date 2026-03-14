using FinanceTracker.Application.Dashboard.DTOs;
using FinanceTracker.Application.Dashboard.Interfaces;
using FinanceTracker.Application.Transactions.DTOs;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Financial;

public sealed class DashboardService(ApplicationDbContext dbContext) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonth = monthStart.AddMonths(1);

        var income = await dbContext.Transactions
            .Where(x => x.UserId == userId && !x.IsDeleted && x.Type == TransactionType.Income && x.DateUtc >= monthStart && x.DateUtc < nextMonth)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var expense = await dbContext.Transactions
            .Where(x => x.UserId == userId && !x.IsDeleted && x.Type == TransactionType.Expense && x.DateUtc >= monthStart && x.DateUtc < nextMonth)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var netBalance = await dbContext.Accounts
            .Where(x => x.UserId == userId && !x.IsArchived)
            .SumAsync(x => (decimal?)x.CurrentBalance, cancellationToken) ?? 0m;

        var recentTransactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .Include(x => x.Account)
            .Include(x => x.TransferAccount)
            .Include(x => x.Category)
            .Include(x => x.Tags)
            .OrderByDescending(x => x.DateUtc)
            .ThenByDescending(x => x.CreatedUtc)
            .Take(8)
            .ToListAsync(cancellationToken);

        var recent = recentTransactions
            .Select(x => new TransactionDto(
                x.Id,
                x.AccountId,
                x.Account.Name,
                x.TransferAccountId,
                x.TransferAccount?.Name,
                x.Type,
                x.Amount,
                x.DateUtc,
                x.CategoryId,
                x.Category?.Name,
                x.Note,
                x.Merchant,
                x.PaymentMethod,
                x.RecurringTransactionId,
                x.Tags.OrderBy(t => t.Value).Select(t => t.Value).ToList(),
                x.CreatedUtc,
                x.UpdatedUtc))
            .ToList();

        var expenseTransactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted && x.Type == TransactionType.Expense && x.DateUtc >= monthStart && x.DateUtc < nextMonth && x.CategoryId != null)
            .Include(x => x.Category)
            .ToListAsync(cancellationToken);

        var spending = expenseTransactions
            .Where(x => x.Category is not null)
            .GroupBy(x => new { x.CategoryId, x.Category!.Name })
            .Select(g => new CategorySpendDto(g.Key.CategoryId!.Value, g.Key.Name, g.Sum(x => x.Amount)))
            .OrderByDescending(x => x.Amount)
            .ToList();

        var budgets = await dbContext.Budgets
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Year == monthStart.Year && x.Month == monthStart.Month)
            .ToListAsync(cancellationToken);

        var actualsByCategory = expenseTransactions
            .GroupBy(x => x.CategoryId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var budgetHealth = new BudgetHealthDto(
            budgets.Sum(x => x.Amount),
            budgets.Sum(x => actualsByCategory.GetValueOrDefault(x.CategoryId)),
            budgets.Sum(x => x.Amount - actualsByCategory.GetValueOrDefault(x.CategoryId)),
            budgets.Count(x => actualsByCategory.GetValueOrDefault(x.CategoryId) > x.Amount),
            budgets.Count(x => x.Amount > 0m && ((actualsByCategory.GetValueOrDefault(x.CategoryId) / x.Amount) * 100m) >= x.AlertThresholdPercent));

        return new DashboardSummaryDto(income, expense, netBalance, recent, spending, budgetHealth);
    }
}
