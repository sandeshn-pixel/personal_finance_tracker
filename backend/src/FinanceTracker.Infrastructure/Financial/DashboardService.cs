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
        var today = now.Date;

        var income = await dbContext.Transactions
            .Where(x => x.UserId == userId && !x.IsDeleted && x.Type == TransactionType.Income && x.DateUtc >= monthStart && x.DateUtc < nextMonth)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var expense = await dbContext.Transactions
            .Where(x => x.UserId == userId && !x.IsDeleted && x.Type == TransactionType.Expense && x.DateUtc >= monthStart && x.DateUtc < nextMonth)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsArchived)
            .OrderByDescending(x => x.CurrentBalance)
            .ToListAsync(cancellationToken);

        var netBalance = accounts.Sum(x => x.CurrentBalance);

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

        var monthTransactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted && x.DateUtc >= monthStart && x.DateUtc < nextMonth)
            .Include(x => x.Category)
            .ToListAsync(cancellationToken);

        var expenseTransactions = monthTransactions
            .Where(x => x.Type == TransactionType.Expense && x.CategoryId != null)
            .ToList();

        var spending = expenseTransactions
            .Where(x => x.Category is not null)
            .GroupBy(x => new { x.CategoryId, x.Category!.Name })
            .Select(g => new CategorySpendDto(g.Key.CategoryId!.Value, g.Key.Name, g.Sum(x => x.Amount)))
            .OrderByDescending(x => x.Amount)
            .ToList();

        var incomeExpenseTrend = BuildIncomeExpenseTrend(monthTransactions, monthStart, nextMonth);

        var accountBalanceDistribution = accounts
            .Select(x => new AccountBalanceSliceDto(x.Id, x.Name, x.Type.ToString(), x.CurrencyCode, x.CurrentBalance))
            .ToList();

        var budgets = await dbContext.Budgets
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.UserId == userId && x.Year == monthStart.Year && x.Month == monthStart.Month)
            .ToListAsync(cancellationToken);

        var actualsByCategory = expenseTransactions
            .GroupBy(x => x.CategoryId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var budgetUsage = budgets
            .Select(x =>
            {
                var spent = actualsByCategory.GetValueOrDefault(x.CategoryId);
                var remaining = x.Amount - spent;
                var usagePercent = x.Amount == 0m ? 0m : decimal.Round((spent / x.Amount) * 100m, 2, MidpointRounding.AwayFromZero);
                return new BudgetUsageItemDto(
                    x.Id,
                    x.CategoryId,
                    x.Category.Name,
                    x.Amount,
                    spent,
                    remaining,
                    usagePercent,
                    spent > x.Amount,
                    x.Amount > 0m && usagePercent >= x.AlertThresholdPercent);
            })
            .OrderByDescending(x => x.UsagePercent)
            .ThenByDescending(x => x.Spent)
            .Take(5)
            .ToList();

        var budgetHealth = new BudgetHealthDto(
            budgets.Sum(x => x.Amount),
            budgets.Sum(x => actualsByCategory.GetValueOrDefault(x.CategoryId)),
            budgets.Sum(x => x.Amount - actualsByCategory.GetValueOrDefault(x.CategoryId)),
            budgets.Count(x => actualsByCategory.GetValueOrDefault(x.CategoryId) > x.Amount),
            budgets.Count(x => x.Amount > 0m && ((actualsByCategory.GetValueOrDefault(x.CategoryId) / x.Amount) * 100m) >= x.AlertThresholdPercent));

        var goalEntries = await dbContext.GoalEntries
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.OccurredAtUtc >= monthStart && x.OccurredAtUtc < nextMonth)
            .ToListAsync(cancellationToken);

        var totalContributed = goalEntries.Where(x => x.Type == GoalEntryType.Contribution).Sum(x => x.Amount);
        var totalWithdrawn = goalEntries.Where(x => x.Type == GoalEntryType.Withdrawal).Sum(x => x.Amount);

        var goals = await dbContext.Goals
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Status != GoalStatus.Archived)
            .Include(x => x.LinkedAccount)
            .OrderByDescending(x => x.Status == GoalStatus.Active)
            .ThenByDescending(x => x.CurrentAmount)
            .Take(4)
            .ToListAsync(cancellationToken);

        var goalProgress = goals
            .Select(x => new GoalProgressDto(
                x.Id,
                x.Name,
                x.Icon,
                x.Color,
                x.CurrentAmount,
                x.TargetAmount,
                x.TargetAmount == 0m ? 0m : decimal.Round((x.CurrentAmount / x.TargetAmount) * 100m, 2, MidpointRounding.AwayFromZero),
                x.LinkedAccount?.Name,
                x.TargetDateUtc,
                x.Status))
            .ToList();

        var activeGoalsCount = await dbContext.Goals.CountAsync(x => x.UserId == userId && x.Status == GoalStatus.Active, cancellationToken);
        var completedGoalsCount = await dbContext.Goals.CountAsync(x => x.UserId == userId && x.Status == GoalStatus.Completed, cancellationToken);
        var activeRecurringRulesCount = await dbContext.RecurringTransactionRules.CountAsync(x => x.UserId == userId && x.Status == RecurringRuleStatus.Active, cancellationToken);
        var pausedRecurringRulesCount = await dbContext.RecurringTransactionRules.CountAsync(x => x.UserId == userId && x.Status == RecurringRuleStatus.Paused, cancellationToken);
        var dueRecurringRulesCount = await dbContext.RecurringTransactionRules.CountAsync(x => x.UserId == userId && x.Status == RecurringRuleStatus.Active && x.NextRunDateUtc != null && x.NextRunDateUtc <= today, cancellationToken);

        var savingsAutomation = new SavingsAutomationSummaryDto(
            totalContributed,
            totalWithdrawn,
            totalContributed - totalWithdrawn,
            activeGoalsCount,
            completedGoalsCount,
            activeRecurringRulesCount,
            pausedRecurringRulesCount,
            dueRecurringRulesCount);

        var recentGoalActivities = await dbContext.GoalEntries
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Include(x => x.Goal)
            .Include(x => x.Account)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.CreatedUtc)
            .Take(6)
            .Select(x => new RecentGoalActivityDto(
                x.Id,
                x.GoalId,
                x.Goal.Name,
                x.Type,
                x.Amount,
                x.OccurredAtUtc,
                x.Note,
                x.Account != null ? x.Account.Name : null))
            .ToListAsync(cancellationToken);

        return new DashboardSummaryDto(
            income,
            expense,
            netBalance,
            recent,
            spending,
            incomeExpenseTrend,
            accountBalanceDistribution,
            goalProgress,
            budgetUsage,
            budgetHealth,
            savingsAutomation,
            recentGoalActivities);
    }

    private static IReadOnlyCollection<TrendPointDto> BuildIncomeExpenseTrend(IReadOnlyCollection<Domain.Entities.Transaction> monthTransactions, DateTime monthStart, DateTime nextMonth)
    {
        var periods = new List<(DateTime Start, DateTime End, string Label)>();
        var cursor = monthStart;
        var week = 1;
        while (cursor < nextMonth)
        {
            var periodEnd = cursor.AddDays(7);
            if (periodEnd > nextMonth)
            {
                periodEnd = nextMonth;
            }

            periods.Add((cursor, periodEnd, $"Week {week}"));
            cursor = periodEnd;
            week++;
        }

        return periods.Select(period => new TrendPointDto(
            period.Label,
            monthTransactions.Where(x => x.Type == TransactionType.Income && x.DateUtc >= period.Start && x.DateUtc < period.End).Sum(x => x.Amount),
            monthTransactions.Where(x => x.Type == TransactionType.Expense && x.DateUtc >= period.Start && x.DateUtc < period.End).Sum(x => x.Amount)))
            .ToList();
    }
}