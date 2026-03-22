using FinanceTracker.Application.Common;
using FinanceTracker.Application.Forecasting.DTOs;
using FinanceTracker.Application.Forecasting.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Financial;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Forecasting;

public sealed class ForecastService(ApplicationDbContext dbContext, TimeProvider timeProvider) : IForecastService
{
    private const int HistoryWindowDays = 90;
    private const int MinimumHistoryTransactionCount = 5;
    private const int MinimumHistorySpanDays = 14;
    private const decimal MediumRiskBalanceRatio = 0.15m;
    private const int MaxRecurringItemsInSummary = 8;

    public async Task<ForecastMonthSummaryDto> GetMonthSummaryAsync(Guid userId, ForecastQuery query, CancellationToken cancellationToken)
    {
        var result = await BuildForecastAsync(userId, query, cancellationToken);
        return result.Summary;
    }

    public async Task<ForecastDailyResponseDto> GetDailyProjectionAsync(Guid userId, ForecastQuery query, CancellationToken cancellationToken)
    {
        var result = await BuildForecastAsync(userId, query, cancellationToken);
        return new ForecastDailyResponseDto(result.Summary, result.Points);
    }

    private async Task<ForecastComputationResult> BuildForecastAsync(Guid userId, ForecastQuery query, CancellationToken cancellationToken)
    {
        var today = DateTime.SpecifyKind(timeProvider.GetUtcNow().UtcDateTime.Date, DateTimeKind.Utc);
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonth = monthStart.AddMonths(1);
        var monthEnd = nextMonth.AddDays(-1);

        var accounts = await ResolveScopedAccountsAsync(userId, query.AccountId, cancellationToken);
        var accountIds = accounts.Select(x => x.Id).ToList();
        var currentBalance = RoundMoney(accounts.Sum(x => x.CurrentBalance));
        var daysRemaining = Math.Max((monthEnd - today).Days + 1, 0);

        var historyStart = today.AddDays(-HistoryWindowDays);
        var historicalTransactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(x => x.UserId == userId
                && !x.IsDeleted
                && x.Type != TransactionType.Transfer
                && x.DateUtc >= historyStart
                && x.DateUtc < today
                && accountIds.Contains(x.AccountId))
            .ToListAsync(cancellationToken);

        var historySpanDays = historicalTransactions.Count == 0
            ? 0
            : Math.Max((today - historicalTransactions.Min(x => x.DateUtc).Date).Days + 1, 1);

        var hasSparseData = historicalTransactions.Count < MinimumHistoryTransactionCount || historySpanDays < MinimumHistorySpanDays;
        var historicalIncome = historicalTransactions.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount);
        var historicalExpense = historicalTransactions.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount);
        var averageDailyIncome = hasSparseData ? 0m : RoundMoney(historicalIncome / HistoryWindowDays);
        var averageDailyExpense = hasSparseData ? 0m : RoundMoney(historicalExpense / HistoryWindowDays);
        var averageDailyNet = RoundMoney(averageDailyIncome - averageDailyExpense);

        var recurringItems = await BuildUpcomingRecurringItemsAsync(userId, query.AccountId, today, nextMonth, cancellationToken);
        var recurringByDate = recurringItems
            .GroupBy(x => x.ScheduledDateUtc)
            .ToDictionary(
                g => g.Key,
                g => RoundMoney(g.Sum(item => item.Type == TransactionType.Income ? item.Amount : -item.Amount)));

        var points = new List<ForecastDayPointDto>();
        var runningBalance = currentBalance;
        var minimumProjectedBalance = currentBalance;

        for (var cursor = today; cursor < nextMonth; cursor = cursor.AddDays(1))
        {
            var recurringNetChange = recurringByDate.GetValueOrDefault(cursor);
            runningBalance = RoundMoney(runningBalance + averageDailyNet + recurringNetChange);
            minimumProjectedBalance = Math.Min(minimumProjectedBalance, runningBalance);
            points.Add(new ForecastDayPointDto(cursor, runningBalance, averageDailyNet, recurringNetChange));
        }

        var projectedEndBalance = points.Count == 0 ? currentBalance : points[^1].ProjectedBalance;
        var safeToSpend = Math.Max(RoundMoney(minimumProjectedBalance), 0m);

        var recurringIncome = recurringItems.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount);
        var recurringExpense = recurringItems.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount);
        var recurringSummary = new ForecastRecurringSummaryDto(
            RoundMoney(recurringIncome),
            RoundMoney(recurringExpense),
            RoundMoney(recurringIncome - recurringExpense),
            recurringItems.Count,
            recurringItems
                .OrderBy(x => x.ScheduledDateUtc)
                .ThenBy(x => x.Title)
                .Take(MaxRecurringItemsInSummary)
                .ToList());

        var riskLevel = DetermineRiskLevel(currentBalance, projectedEndBalance, minimumProjectedBalance, safeToSpend, hasSparseData);
        var notes = BuildNotes(hasSparseData, recurringSummary, currentBalance, projectedEndBalance, minimumProjectedBalance, safeToSpend);
        var basisDescription = hasSparseData
            ? "Forecast uses known recurring items only because recent history is limited."
            : $"Forecast blends the last {HistoryWindowDays} days of income and expense activity with known recurring items due before month end.";

        var summary = new ForecastMonthSummaryDto(
            currentBalance,
            RoundMoney(projectedEndBalance),
            RoundMoney(minimumProjectedBalance),
            RoundMoney(safeToSpend),
            averageDailyIncome,
            averageDailyExpense,
            averageDailyNet,
            daysRemaining,
            hasSparseData,
            riskLevel,
            basisDescription,
            recurringSummary,
            notes);

        return new ForecastComputationResult(summary, points);
    }

    private async Task<List<Account>> ResolveScopedAccountsAsync(Guid userId, Guid? accountId, CancellationToken cancellationToken)
    {
        var query = dbContext.Accounts
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsArchived);

        if (accountId.HasValue)
        {
            query = query.Where(x => x.Id == accountId.Value);
        }

        var accounts = await query
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        if (accountId.HasValue && accounts.Count == 0)
        {
            throw new NotFoundException("Account was not found.");
        }

        return accounts;
    }

    private async Task<List<ForecastRecurringItemDto>> BuildUpcomingRecurringItemsAsync(Guid userId, Guid? accountId, DateTime today, DateTime nextMonth, CancellationToken cancellationToken)
    {
        var rules = await dbContext.RecurringTransactionRules
            .AsNoTracking()
            .Where(x => x.UserId == userId
                && x.Status == RecurringRuleStatus.Active
                && x.Type != TransactionType.Transfer
                && (!accountId.HasValue || x.AccountId == accountId.Value))
            .Include(x => x.Account)
            .Include(x => x.Executions)
            .OrderBy(x => x.NextRunDateUtc)
            .ToListAsync(cancellationToken);

        if (rules.Count == 0)
        {
            return [];
        }

        var ruleIds = rules.Select(x => x.Id).ToList();
        var materializedOccurrences = await dbContext.Transactions
            .AsNoTracking()
            .Where(x => x.UserId == userId
                && !x.IsDeleted
                && x.RecurringTransactionId.HasValue
                && ruleIds.Contains(x.RecurringTransactionId.Value)
                && x.DateUtc >= today
                && x.DateUtc < nextMonth)
            .Select(x => new { RuleId = x.RecurringTransactionId!.Value, ScheduledDateUtc = x.DateUtc })
            .ToListAsync(cancellationToken);

        var materializedLookup = materializedOccurrences
            .Select(x => (x.RuleId, x.ScheduledDateUtc))
            .ToHashSet();

        var recurringItems = new List<ForecastRecurringItemDto>();

        foreach (var rule in rules)
        {
            foreach (var scheduledDate in EnumerateExpectedOccurrences(rule, today, nextMonth))
            {
                if (materializedLookup.Contains((rule.Id, scheduledDate)))
                {
                    continue;
                }

                recurringItems.Add(new ForecastRecurringItemDto(
                    scheduledDate,
                    rule.Title,
                    rule.Type,
                    RoundMoney(rule.Amount),
                    rule.Account.Name));
            }
        }

        return recurringItems;
    }

    private static IEnumerable<DateTime> EnumerateExpectedOccurrences(RecurringTransactionRule rule, DateTime startInclusive, DateTime monthEndExclusive)
    {
        var next = rule.NextRunDateUtc.HasValue
            ? RecurringScheduleCalculator.NormalizeDate(rule.NextRunDateUtc.Value)
            : RecurringScheduleCalculator.RecalculateNextRunDate(rule, rule.Executions);

        while (next.HasValue && next.Value < monthEndExclusive)
        {
            if (next.Value >= startInclusive)
            {
                var alreadyCompleted = rule.Executions.Any(x =>
                    x.ScheduledForDateUtc == next.Value
                    && x.Status is RecurringExecutionStatus.Completed or RecurringExecutionStatus.Reminded);

                if (!alreadyCompleted)
                {
                    yield return next.Value;
                }
            }

            var upcoming = RecurringScheduleCalculator.CalculateNextOccurrence(rule.Frequency, next.Value);
            if (rule.EndDateUtc.HasValue && upcoming > RecurringScheduleCalculator.NormalizeDate(rule.EndDateUtc.Value))
            {
                yield break;
            }

            next = upcoming;
        }
    }

    private static ForecastRiskLevel DetermineRiskLevel(decimal currentBalance, decimal projectedEndBalance, decimal minimumProjectedBalance, decimal safeToSpend, bool hasSparseData)
    {
        if (projectedEndBalance < 0m || minimumProjectedBalance < 0m)
        {
            return ForecastRiskLevel.High;
        }

        if (safeToSpend <= 0m || hasSparseData || (currentBalance > 0m && minimumProjectedBalance <= RoundMoney(currentBalance * MediumRiskBalanceRatio)))
        {
            return ForecastRiskLevel.Medium;
        }

        return ForecastRiskLevel.Low;
    }

    private static IReadOnlyCollection<string> BuildNotes(bool hasSparseData, ForecastRecurringSummaryDto recurringSummary, decimal currentBalance, decimal projectedEndBalance, decimal minimumProjectedBalance, decimal safeToSpend)
    {
        var notes = new List<string>();

        if (hasSparseData)
        {
            notes.Add("Recent transaction history is limited, so the forecast leans on known recurring items and may be conservative.");
        }

        if (recurringSummary.TotalExpectedExpense > 0m)
        {
            notes.Add($"Known recurring outflows before month end total {RoundMoney(recurringSummary.TotalExpectedExpense):0.00}.");
        }

        if (minimumProjectedBalance < currentBalance)
        {
            notes.Add("Projected balances dip below your current balance during the remainder of the month.");
        }

        if (projectedEndBalance < 0m)
        {
            notes.Add("At the current pace, you are likely to end the month below zero.");
        }
        else if (safeToSpend <= 0m)
        {
            notes.Add("There is no additional discretionary cushion without increasing the risk of a negative balance.");
        }

        return notes;
    }

    private static decimal RoundMoney(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record ForecastComputationResult(
        ForecastMonthSummaryDto Summary,
        IReadOnlyCollection<ForecastDayPointDto> Points);
}
