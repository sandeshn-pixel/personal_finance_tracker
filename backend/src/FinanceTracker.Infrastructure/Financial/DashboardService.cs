using FinanceTracker.Application.Common;
using FinanceTracker.Application.Dashboard.DTOs;
using FinanceTracker.Application.Dashboard.Interfaces;
using FinanceTracker.Application.Transactions.DTOs;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Financial;

public sealed class DashboardService(
    ApplicationDbContext dbContext,
    AccountAccessService accountAccessService) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(Guid userId, DashboardQuery query, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonth = monthStart.AddMonths(1);
        var today = now.Date;

        var accounts = (await LoadSelectedAccountsAsync(userId, ResolveRequestedAccountIds(query), cancellationToken))
            .OrderByDescending(x => x.CurrentBalance)
            .ToList();

        var selectedAccountIds = accounts.Select(x => x.Id).ToList();
        var filterBySelectedAccounts = selectedAccountIds.Count > 0;
        var includeOwnedPlanning = accounts.Any(x => x.UserId == userId);
        var sharedAccounts = accounts
            .Where(x => x.UserId != userId)
            .Select(x => (x.Id, x.UserId))
            .Distinct()
            .ToList();

        var netBalance = accounts.Sum(x => x.CurrentBalance);

        var recentTransactions = await dbContext.Transactions
            .AsNoTracking()
            .WhereUserCanView(userId)
            .Where(x => !filterBySelectedAccounts || selectedAccountIds.Contains(x.AccountId) || (x.TransferAccountId.HasValue && selectedAccountIds.Contains(x.TransferAccountId.Value)))
            .Include(x => x.Account)
            .Include(x => x.TransferAccount)
            .Include(x => x.Category)
            .Include(x => x.Tags)
            .Include(x => x.CreatedByUser)
            .Include(x => x.UpdatedByUser)
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
                x.CreatedByUserId,
                BuildDisplayName(x.CreatedByUser),
                x.UpdatedByUserId,
                BuildDisplayName(x.UpdatedByUser),
                x.CreatedUtc,
                x.UpdatedUtc))
            .ToList();

        var monthTransactions = await dbContext.Transactions
            .AsNoTracking()
            .WhereUserCanView(userId)
            .Where(x => x.DateUtc >= monthStart && x.DateUtc < nextMonth)
            .Where(x => !filterBySelectedAccounts || selectedAccountIds.Contains(x.AccountId) || (x.TransferAccountId.HasValue && selectedAccountIds.Contains(x.TransferAccountId.Value)))
            .Include(x => x.Category)
            .ToListAsync(cancellationToken);

        var income = monthTransactions
            .Where(x => x.Type == TransactionType.Income)
            .Sum(x => x.Amount);

        var expense = monthTransactions
            .Where(x => x.Type == TransactionType.Expense)
            .Sum(x => x.Amount);

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

        var budgets = await LoadVisibleBudgetsAsync(userId, monthStart.Year, monthStart.Month, includeOwnedPlanning, sharedAccounts, cancellationToken);
        var actualsByBudget = await LoadBudgetActualsAsync(userId, monthStart.Year, monthStart.Month, includeOwnedPlanning, sharedAccounts, cancellationToken);

        var budgetUsage = budgets
            .Select(x =>
            {
                var spent = actualsByBudget.GetValueOrDefault((x.UserId, x.CategoryId));
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
                    x.Amount > 0m && usagePercent >= x.AlertThresholdPercent,
                    x.UserId == userId,
                    BuildDisplayName(x.User));
            })
            .OrderByDescending(x => x.UsagePercent)
            .ThenByDescending(x => x.Spent)
            .Take(5)
            .ToList();

        var sharedReadOnlyBudgetCount = budgets.Count(x => x.UserId != userId);
        var budgetHealth = new BudgetHealthDto(
            budgets.Sum(x => x.Amount),
            budgets.Sum(x => actualsByBudget.GetValueOrDefault((x.UserId, x.CategoryId))),
            budgets.Sum(x => x.Amount - actualsByBudget.GetValueOrDefault((x.UserId, x.CategoryId))),
            budgets.Count(x => actualsByBudget.GetValueOrDefault((x.UserId, x.CategoryId)) > x.Amount),
            budgets.Count(x => x.Amount > 0m && ((actualsByBudget.GetValueOrDefault((x.UserId, x.CategoryId)) / x.Amount) * 100m) >= x.AlertThresholdPercent),
            sharedReadOnlyBudgetCount,
            budgets.Where(x => x.UserId != userId).Select(x => x.UserId).Distinct().Count());

        List<GoalEntry> goalEntries = [];
        List<Goal> goals = [];
        List<RecentGoalActivityDto> recentGoalActivities = [];
        var activeGoalsCount = 0;
        var completedGoalsCount = 0;
        var activeRecurringRulesCount = 0;
        var pausedRecurringRulesCount = 0;
        var dueRecurringRulesCount = 0;

        if (includeOwnedPlanning)
        {
            goalEntries = await dbContext.GoalEntries
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.OccurredAtUtc >= monthStart && x.OccurredAtUtc < nextMonth)
                .ToListAsync(cancellationToken);

            goals = (await dbContext.Goals
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.Status != GoalStatus.Archived)
                .Include(x => x.LinkedAccount)
                .ToListAsync(cancellationToken))
                .OrderByDescending(x => x.Status == GoalStatus.Active)
                .ThenByDescending(x => x.CurrentAmount)
                .Take(4)
                .ToList();

            activeGoalsCount = await dbContext.Goals.CountAsync(x => x.UserId == userId && x.Status == GoalStatus.Active, cancellationToken);
            completedGoalsCount = await dbContext.Goals.CountAsync(x => x.UserId == userId && x.Status == GoalStatus.Completed, cancellationToken);
            activeRecurringRulesCount = await dbContext.RecurringTransactionRules.CountAsync(x => x.UserId == userId && x.Status == RecurringRuleStatus.Active, cancellationToken);
            pausedRecurringRulesCount = await dbContext.RecurringTransactionRules.CountAsync(x => x.UserId == userId && x.Status == RecurringRuleStatus.Paused, cancellationToken);
            dueRecurringRulesCount = await dbContext.RecurringTransactionRules.CountAsync(x => x.UserId == userId && x.Status == RecurringRuleStatus.Active && x.NextRunDateUtc != null && x.NextRunDateUtc <= today, cancellationToken);

            recentGoalActivities = await dbContext.GoalEntries
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
        }

        var totalContributed = goalEntries.Where(x => x.Type == GoalEntryType.Contribution).Sum(x => x.Amount);
        var totalWithdrawn = goalEntries.Where(x => x.Type == GoalEntryType.Withdrawal).Sum(x => x.Amount);

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

        var savingsAutomation = new SavingsAutomationSummaryDto(
            totalContributed,
            totalWithdrawn,
            totalContributed - totalWithdrawn,
            activeGoalsCount,
            completedGoalsCount,
            activeRecurringRulesCount,
            pausedRecurringRulesCount,
            dueRecurringRulesCount);

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

    private static IReadOnlyCollection<Guid> ResolveRequestedAccountIds(DashboardQuery query)
    {
        if (query.AccountIds is { Length: > 0 })
        {
            return query.AccountIds.Distinct().ToArray();
        }

        return query.AccountId.HasValue ? [query.AccountId.Value] : [];
    }

    private async Task<List<Account>> LoadSelectedAccountsAsync(Guid userId, IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken)
    {
        if (accountIds.Count == 0)
        {
            return await accountAccessService.QueryAccessibleAccounts(userId, AccountMemberRole.Viewer, includeArchived: false)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        var accessibleAccounts = await accountAccessService.QueryAccessibleAccounts(userId, AccountMemberRole.Viewer, includeArchived: false)
            .AsNoTracking()
            .Where(x => accountIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (accessibleAccounts.Count != accountIds.Count)
        {
            throw new NotFoundException("One or more accounts were not found.");
        }

        return accessibleAccounts;
    }

    private async Task<List<Budget>> LoadVisibleBudgetsAsync(
        Guid userId,
        int year,
        int month,
        bool includeOwnBudgets,
        IReadOnlyCollection<(Guid AccountId, Guid OwnerUserId)> sharedAccounts,
        CancellationToken cancellationToken)
    {
        List<Budget> ownBudgets;
        if (includeOwnBudgets)
        {
            ownBudgets = await dbContext.Budgets
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.Year == year && x.Month == month)
                .Include(x => x.Category)
                .Include(x => x.User)
                .OrderBy(x => x.Category.Name)
                .ToListAsync(cancellationToken);
        }
        else
        {
            ownBudgets = [];
        }

        if (sharedAccounts.Count == 0)
        {
            return ownBudgets;
        }

        var sharedAccountIds = sharedAccounts.Select(x => x.AccountId).Distinct().ToList();
        var sharedOwnerIds = sharedAccounts.Select(x => x.OwnerUserId).Distinct().ToList();

        var sharedBudgetIds = await dbContext.Transactions
            .AsNoTracking()
            .Where(x => !x.IsDeleted
                && x.Type == TransactionType.Expense
                && x.CategoryId != null
                && x.DateUtc.Year == year
                && x.DateUtc.Month == month
                && sharedAccountIds.Contains(x.AccountId)
                && sharedOwnerIds.Contains(x.UserId))
            .Select(x => new { x.UserId, CategoryId = x.CategoryId!.Value })
            .Distinct()
            .Join(
                dbContext.Budgets.AsNoTracking().Where(x => x.Year == year && x.Month == month && sharedOwnerIds.Contains(x.UserId)),
                transaction => new { transaction.UserId, transaction.CategoryId },
                budget => new { budget.UserId, budget.CategoryId },
                (_, budget) => budget.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (sharedBudgetIds.Count == 0)
        {
            return ownBudgets;
        }

        var sharedBudgets = await dbContext.Budgets
            .AsNoTracking()
            .Where(x => sharedBudgetIds.Contains(x.Id))
            .Include(x => x.Category)
            .Include(x => x.User)
            .ToListAsync(cancellationToken);

        return ownBudgets
            .Concat(sharedBudgets)
            .OrderBy(x => x.UserId == userId ? 0 : 1)
            .ThenBy(x => BuildDisplayName(x.User))
            .ThenBy(x => x.Category.Name)
            .ToList();
    }

    private async Task<Dictionary<(Guid UserId, Guid CategoryId), decimal>> LoadBudgetActualsAsync(
        Guid userId,
        int year,
        int month,
        bool includeOwnActuals,
        IReadOnlyCollection<(Guid AccountId, Guid OwnerUserId)> sharedAccounts,
        CancellationToken cancellationToken)
    {
        var actuals = new Dictionary<(Guid UserId, Guid CategoryId), decimal>();

        if (includeOwnActuals)
        {
            var ownRows = await dbContext.Transactions
                .AsNoTracking()
                .Where(x => x.UserId == userId
                    && !x.IsDeleted
                    && x.Type == TransactionType.Expense
                    && x.CategoryId != null
                    && x.DateUtc.Year == year
                    && x.DateUtc.Month == month)
                .Select(x => new { x.UserId, CategoryId = x.CategoryId!.Value, x.Amount })
                .ToListAsync(cancellationToken);

            foreach (var row in ownRows)
            {
                var key = (row.UserId, row.CategoryId);
                actuals[key] = actuals.GetValueOrDefault(key) + row.Amount;
            }
        }

        if (sharedAccounts.Count == 0)
        {
            return actuals;
        }

        var sharedAccountIds = sharedAccounts.Select(x => x.AccountId).Distinct().ToList();
        var sharedOwnerIds = sharedAccounts.Select(x => x.OwnerUserId).Distinct().ToList();

        var sharedRows = await dbContext.Transactions
            .AsNoTracking()
            .Where(x => !x.IsDeleted
                && x.Type == TransactionType.Expense
                && x.CategoryId != null
                && x.DateUtc.Year == year
                && x.DateUtc.Month == month
                && sharedAccountIds.Contains(x.AccountId)
                && sharedOwnerIds.Contains(x.UserId))
            .Select(x => new { x.UserId, CategoryId = x.CategoryId!.Value, x.Amount })
            .ToListAsync(cancellationToken);

        foreach (var row in sharedRows)
        {
            var key = (row.UserId, row.CategoryId);
            actuals[key] = actuals.GetValueOrDefault(key) + row.Amount;
        }

        return actuals;
    }

    private static string BuildDisplayName(User user)
    {
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }

    private static IReadOnlyCollection<TrendPointDto> BuildIncomeExpenseTrend(IReadOnlyCollection<Transaction> monthTransactions, DateTime monthStart, DateTime nextMonth)
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

