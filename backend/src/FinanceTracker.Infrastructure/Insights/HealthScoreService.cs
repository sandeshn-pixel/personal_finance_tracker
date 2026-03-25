using FinanceTracker.Application.Common;
using FinanceTracker.Application.Insights.DTOs;
using FinanceTracker.Application.Insights.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Financial;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Insights;

public sealed class HealthScoreService(
    ApplicationDbContext dbContext,
    TimeProvider timeProvider,
    AccountAccessService accountAccessService) : IHealthScoreService
{
    private const int LookbackMonths = 3;
    private const int SavingsWeight = 30;
    private const int StabilityWeight = 20;
    private const int BudgetWeight = 20;
    private const int BufferWeight = 30;

    public async Task<HealthScoreResponseDto> GetAsync(Guid userId, HealthScoreQuery query, CancellationToken cancellationToken)
    {
        var today = DateTime.SpecifyKind(timeProvider.GetUtcNow().UtcDateTime.Date, DateTimeKind.Utc);
        var currentMonthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lookbackStart = currentMonthStart.AddMonths(-LookbackMonths);
        var lookbackEnd = currentMonthStart.AddDays(-1);

        var activeAccounts = await ResolveScopedAccountsAsync(userId, ResolveRequestedAccountIds(query), cancellationToken);
        var activeAccountIds = activeAccounts.Select(x => x.Id).ToHashSet();
        var currentBalance = activeAccounts.Sum(x => x.CurrentBalance);
        var includesSharedGuestAccounts = activeAccounts.Any(x => x.UserId != userId);

        var transactions = await dbContext.Transactions
            .AsNoTracking()
            .WhereUserCanView(userId)
            .Where(x => x.Type != TransactionType.Transfer
                && x.DateUtc >= lookbackStart
                && x.DateUtc < currentMonthStart
                && activeAccountIds.Contains(x.AccountId))
            .ToListAsync(cancellationToken);

        List<Budget> budgets;
        if (includesSharedGuestAccounts)
        {
            budgets = [];
        }
        else
        {
            budgets = (await dbContext.Budgets
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .ToListAsync(cancellationToken))
                .Where(x =>
                {
                    var monthStart = new DateTime(x.Year, x.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    return monthStart >= lookbackStart && monthStart < currentMonthStart;
                })
                .ToList();
        }

        var monthStarts = Enumerable.Range(0, LookbackMonths)
            .Select(offset => lookbackStart.AddMonths(offset))
            .ToList();

        var totalIncome = transactions.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount);
        var totalExpense = transactions.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount);
        var monthlyExpenseTotals = monthStarts
            .Select(monthStart => new
            {
                MonthStart = monthStart,
                Expense = transactions.Where(x => x.Type == TransactionType.Expense && x.DateUtc >= monthStart && x.DateUtc < monthStart.AddMonths(1)).Sum(x => x.Amount),
            })
            .ToList();

        var budgetedExpenseByMonthCategory = budgets
            .GroupBy(x => (MonthStart: new DateTime(x.Year, x.Month, 1, 0, 0, 0, DateTimeKind.Utc), x.CategoryId))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var actualExpenseByMonthCategory = transactions
            .Where(x => x.Type == TransactionType.Expense && x.CategoryId.HasValue)
            .GroupBy(x => (MonthStart: new DateTime(x.DateUtc.Year, x.DateUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc), CategoryId: x.CategoryId!.Value))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var savingsFactor = BuildSavingsRateFactor(totalIncome, totalExpense);
        var stabilityFactor = BuildExpenseStabilityFactor(monthlyExpenseTotals.Select(x => x.Expense).ToList());
        var budgetFactor = includesSharedGuestAccounts
            ? new FactorComputation("budget-adherence", "Budget adherence", 60, BudgetWeight, 0m, "Shared-account activity is included in this scope, so budget adherence stays neutral until budgets are fully collaborative.", true)
            : BuildBudgetAdherenceFactor(totalExpense, budgetedExpenseByMonthCategory, actualExpenseByMonthCategory);
        var bufferFactor = BuildCashBufferFactor(currentBalance, monthlyExpenseTotals.Select(x => x.Expense).ToList());

        var factors = new[] { savingsFactor, stabilityFactor, budgetFactor, bufferFactor }
            .Select(MapFactor)
            .ToList();

        var totalWeightedPoints = factors.Sum(x => x.WeightedPoints);
        var overallScore = Math.Clamp((int)Math.Round(totalWeightedPoints, MidpointRounding.AwayFromZero), 0, 100);
        var hasSparseData = factors.Any(x => x.IsFallback);
        var band = ResolveBand(overallScore);
        var suggestions = BuildSuggestions(factors);
        var summary = BuildSummary(overallScore, band, hasSparseData, factors);

        return new HealthScoreResponseDto(
            overallScore,
            band,
            hasSparseData,
            lookbackStart,
            lookbackEnd,
            summary,
            factors,
            suggestions);
    }

    private static IReadOnlyCollection<Guid> ResolveRequestedAccountIds(HealthScoreQuery query)
    {
        if (query.AccountIds is { Length: > 0 })
        {
            return query.AccountIds.Distinct().ToArray();
        }

        return query.AccountId.HasValue ? [query.AccountId.Value] : [];
    }

    private async Task<List<Account>> ResolveScopedAccountsAsync(Guid userId, IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken)
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

    private static FactorComputation BuildSavingsRateFactor(decimal totalIncome, decimal totalExpense)
    {
        if (totalIncome <= 0m && totalExpense <= 0m)
        {
            return new FactorComputation("savings-rate", "Savings rate", 55, SavingsWeight, 0m, "No income history in the lookback window.", true);
        }

        if (totalIncome <= 0m)
        {
            return new FactorComputation("savings-rate", "Savings rate", 0, SavingsWeight, -100m, "Expenses were recorded without offsetting income in the lookback window.", false);
        }

        var savingsRatePercent = decimal.Round(((totalIncome - totalExpense) / totalIncome) * 100m, 2, MidpointRounding.AwayFromZero);
        var normalized = Math.Clamp(savingsRatePercent / 20m, 0m, 1m);
        var score = (int)Math.Round(normalized * 100m, MidpointRounding.AwayFromZero);
        var explanation = savingsRatePercent >= 20m
            ? "You are retaining at least one-fifth of recent income after expenses, which is a strong savings pattern."
            : savingsRatePercent > 0m
                ? "You are saving part of recent income, but there is room to keep a larger portion after expenses."
                : "Recent expenses consumed all income or exceeded it, leaving no savings margin.";

        return new FactorComputation("savings-rate", "Savings rate", score, SavingsWeight, savingsRatePercent, explanation, false);
    }

    private static FactorComputation BuildExpenseStabilityFactor(IReadOnlyCollection<decimal> monthlyExpenses)
    {
        var monthsWithExpense = monthlyExpenses.Count(x => x > 0m);
        if (monthsWithExpense < 2)
        {
            return new FactorComputation("expense-stability", "Expense stability", 60, StabilityWeight, 0m, "There is not enough month-to-month expense history yet, so this factor stays neutral.", true);
        }

        var mean = monthlyExpenses.Average();
        if (mean <= 0m)
        {
            return new FactorComputation("expense-stability", "Expense stability", 60, StabilityWeight, 0m, "Monthly expense history is too light to judge stability reliably.", true);
        }

        var variance = monthlyExpenses.Average(value => DecimalMath.Square(value - mean));
        var standardDeviation = DecimalMath.Sqrt(variance);
        var coefficientOfVariation = decimal.Round((standardDeviation / mean) * 100m, 2, MidpointRounding.AwayFromZero);
        var score = Math.Clamp(100 - (int)Math.Round(coefficientOfVariation, MidpointRounding.AwayFromZero), 0, 100);
        var explanation = coefficientOfVariation <= 15m
            ? "Recent monthly expenses are fairly consistent, which improves predictability."
            : coefficientOfVariation <= 35m
                ? "Recent expenses show some variation, but they remain within a manageable range."
                : "Recent monthly spending swings are wide, which makes planning less predictable.";

        return new FactorComputation("expense-stability", "Expense stability", score, StabilityWeight, coefficientOfVariation, explanation, false);
    }

    private static FactorComputation BuildBudgetAdherenceFactor(
        decimal totalExpense,
        IReadOnlyDictionary<(DateTime MonthStart, Guid CategoryId), decimal> budgetedExpenseByMonthCategory,
        IReadOnlyDictionary<(DateTime MonthStart, Guid CategoryId), decimal> actualExpenseByMonthCategory)
    {
        if (budgetedExpenseByMonthCategory.Count == 0)
        {
            return new FactorComputation("budget-adherence", "Budget adherence", 60, BudgetWeight, 0m, "No recent budgets were found, so this factor stays neutral until budget coverage is added.", true);
        }

        var totalBudgeted = budgetedExpenseByMonthCategory.Values.Sum();
        var budgetedActualExpense = budgetedExpenseByMonthCategory.Keys.Sum(key => actualExpenseByMonthCategory.GetValueOrDefault(key));
        var utilizationPercent = totalBudgeted <= 0m
            ? 100m
            : decimal.Round((budgetedActualExpense / totalBudgeted) * 100m, 2, MidpointRounding.AwayFromZero);
        var adherenceScore = totalBudgeted <= 0m
            ? 60
            : Math.Clamp(100 - (int)Math.Round(Math.Max(utilizationPercent - 100m, 0m) / 50m * 90m, MidpointRounding.AwayFromZero), 10, 100);

        var coveragePercent = totalExpense <= 0m
            ? 100m
            : decimal.Round((budgetedActualExpense / totalExpense) * 100m, 2, MidpointRounding.AwayFromZero);
        var coverageScore = 50m + (Math.Clamp(coveragePercent, 0m, 100m) / 100m * 50m);
        var finalScore = (int)Math.Round((adherenceScore * 0.75m) + (coverageScore * 0.25m), MidpointRounding.AwayFromZero);

        var explanation = utilizationPercent <= 100m
            ? "Recent spending in budgeted categories is staying within plan."
            : utilizationPercent <= 115m
                ? "Recent spending is slightly above plan in budgeted categories."
                : "Recent budgeted spending is materially above plan, which is lowering this factor.";

        return new FactorComputation("budget-adherence", "Budget adherence", finalScore, BudgetWeight, utilizationPercent, explanation, false);
    }

    private static FactorComputation BuildCashBufferFactor(decimal currentBalance, IReadOnlyCollection<decimal> monthlyExpenses)
    {
        var averageMonthlyExpense = monthlyExpenses.Count == 0 ? 0m : monthlyExpenses.Average();
        if (averageMonthlyExpense <= 0m)
        {
            var fallbackScore = currentBalance > 0m ? 65 : 40;
            var fallbackMetric = currentBalance > 0m ? 0m : -1m;
            var fallbackExplanation = currentBalance > 0m
                ? "There is not enough spending history to estimate a cash buffer reliably, so this factor stays slightly positive."
                : "Cash reserves are low and spending history is too limited to estimate a buffer confidently.";

            return new FactorComputation("cash-buffer", "Cash buffer", fallbackScore, BufferWeight, fallbackMetric, fallbackExplanation, true);
        }

        var monthsCovered = decimal.Round(currentBalance / averageMonthlyExpense, 2, MidpointRounding.AwayFromZero);
        var normalized = Math.Clamp(monthsCovered / 3m, 0m, 1m);
        var score = (int)Math.Round(normalized * 100m, MidpointRounding.AwayFromZero);
        var explanation = monthsCovered >= 3m
            ? "Current active-account balances cover at least three months of recent average expenses."
            : monthsCovered >= 1m
                ? "Current balances cover more than one month of recent average expenses, but the buffer is still modest."
                : "Current balances cover less than one month of recent average expenses, which leaves a thin cash buffer.";

        return new FactorComputation("cash-buffer", "Cash buffer", score, BufferWeight, monthsCovered, explanation, false);
    }

    private static HealthScoreFactorDto MapFactor(FactorComputation factor)
    {
        var weightedPoints = decimal.Round((factor.Score / 100m) * factor.WeightPercent, 2, MidpointRounding.AwayFromZero);
        var metricLabel = factor.Key switch
        {
            "savings-rate" => "Savings rate %",
            "expense-stability" => "Expense volatility %",
            "budget-adherence" => "Budget use %",
            "cash-buffer" => "Months of expenses covered",
            _ => "Metric"
        };

        return new HealthScoreFactorDto(factor.Key, factor.Title, factor.Score, factor.WeightPercent, weightedPoints, factor.MetricValue, metricLabel, factor.Explanation, factor.IsFallback);
    }

    private static HealthScoreBand ResolveBand(int score)
        => score switch
        {
            < 40 => HealthScoreBand.Poor,
            < 60 => HealthScoreBand.Fair,
            < 80 => HealthScoreBand.Good,
            _ => HealthScoreBand.Strong
        };

    private static IReadOnlyCollection<string> BuildSuggestions(IReadOnlyCollection<HealthScoreFactorDto> factors)
    {
        var suggestions = new List<string>();

        foreach (var factor in factors.Where(x => x.Score < 60).OrderBy(x => x.Score))
        {
            switch (factor.Key)
            {
                case "savings-rate":
                    suggestions.Add("Try to keep a larger share of income after regular expenses to improve savings resilience.");
                    break;
                case "expense-stability":
                    suggestions.Add("Review months with unusual spikes and plan irregular expenses ahead of time to reduce volatility.");
                    break;
                case "budget-adherence":
                    suggestions.Add("Add or refine budgets for major expense categories and review categories that are regularly over plan.");
                    break;
                case "cash-buffer":
                    suggestions.Add("Building a larger cash reserve in active accounts would improve day-to-day resilience.");
                    break;
            }
        }

        return suggestions.Count > 0
            ? suggestions.Distinct(StringComparer.Ordinal).Take(3).ToList()
            : ["Your score is steady. Keep recording transactions and budgets to make the view even more reliable."];
    }

    private static string BuildSummary(int score, HealthScoreBand band, bool hasSparseData, IReadOnlyCollection<HealthScoreFactorDto> factors)
    {
        var weakestFactor = factors.OrderBy(x => x.Score).First();
        var summary = $"Your financial health score is {score}/100, which currently lands in the {band.ToString().ToLowerInvariant()} range.";

        if (hasSparseData)
        {
            return $"{summary} Some factors are using neutral fallback behavior because recent data is limited.";
        }

        return $"{summary} The weakest area right now is {weakestFactor.Title.ToLowerInvariant()}.";
    }

    private sealed record FactorComputation(string Key, string Title, int Score, int WeightPercent, decimal MetricValue, string Explanation, bool IsFallback);
}

internal static class DecimalMath
{
    public static decimal Sqrt(decimal value)
    {
        if (value <= 0m)
        {
            return 0m;
        }

        var current = (decimal)Math.Sqrt((double)value);
        if (current == 0m)
        {
            current = 1m;
        }

        for (var i = 0; i < 8; i++)
        {
            current = (current + (value / current)) / 2m;
        }

        return current;
    }

    public static decimal Square(decimal value) => value * value;
}

