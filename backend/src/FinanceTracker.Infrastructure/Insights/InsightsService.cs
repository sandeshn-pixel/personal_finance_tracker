using FinanceTracker.Application.Common;
using FinanceTracker.Application.Insights.DTOs;
using FinanceTracker.Application.Insights.Interfaces;
using FinanceTracker.Application.Reports.DTOs;
using FinanceTracker.Application.Reports.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Financial;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Insights;

public sealed class InsightsService(
    ApplicationDbContext dbContext,
    AccountAccessService accountAccessService,
    IReportService reportService) : IInsightsService
{
    private const decimal MaterialPercentChange = 10m;
    private const decimal MaterialCategoryAmount = 250m;
    private const decimal MaterialIncomeAmount = 500m;
    private const decimal MaterialNetWorthAmount = 500m;
    private const decimal MaterialSavingsRateChange = 5m;
    private const decimal MaterialBudgetChange = 5m;

    public async Task<InsightsResponseDto> GetAsync(Guid userId, InsightsQuery query, CancellationToken cancellationToken)
    {
        var start = NormalizeDate(query.StartDateUtc);
        var endExclusive = NormalizeDate(query.EndDateUtc).AddDays(1);
        var periodLength = endExclusive - start;
        var comparisonStart = start - periodLength;
        var comparisonEndExclusive = start;

        var accounts = await ResolveScopedAccountsAsync(userId, ResolveRequestedIds(query.AccountIds, query.AccountId), cancellationToken);
        var accountIds = accounts.Select(x => x.Id).ToHashSet();
        var includesSharedGuestAccounts = accounts.Any(x => x.UserId != userId);
        var categoryFilterIds = ResolveRequestedIds(query.CategoryIds, query.CategoryId).ToHashSet();

        var currentTransactions = await dbContext.Transactions
            .AsNoTracking()
            .WhereUserCanView(userId)
            .Where(x => x.Type != TransactionType.Transfer
                && x.DateUtc >= start
                && x.DateUtc < endExclusive
                && accountIds.Contains(x.AccountId))
            .Include(x => x.Category)
            .ToListAsync(cancellationToken);

        var comparisonTransactions = await dbContext.Transactions
            .AsNoTracking()
            .WhereUserCanView(userId)
            .Where(x => x.Type != TransactionType.Transfer
                && x.DateUtc >= comparisonStart
                && x.DateUtc < comparisonEndExclusive
                && accountIds.Contains(x.AccountId))
            .Include(x => x.Category)
            .ToListAsync(cancellationToken);

        var insights = new List<InsightItemDto>();
        AddCategorySpendingInsight(insights, currentTransactions, comparisonTransactions, categoryFilterIds, start, endExclusive, comparisonStart, comparisonEndExclusive);
        AddSavingsRateInsight(insights, currentTransactions, comparisonTransactions, start, endExclusive, comparisonStart, comparisonEndExclusive);
        AddIncomeInsight(insights, currentTransactions, comparisonTransactions, start, endExclusive, comparisonStart, comparisonEndExclusive);
        await AddBudgetInsightAsync(insights, userId, includesSharedGuestAccounts, currentTransactions, comparisonTransactions, start, endExclusive, comparisonStart, comparisonEndExclusive, cancellationToken);
        await AddNetWorthInsightAsync(insights, userId, query, start, endExclusive, cancellationToken);

        var hasSparseData = currentTransactions.Count < 3 || comparisonTransactions.Count < 3;
        if (insights.Count == 0)
        {
            insights.Add(new InsightItemDto(
                "steady-patterns",
                "Patterns are steady",
                "Income, expenses, and net worth are broadly steady versus the prior period.",
                BuildBasis(start, endExclusive, comparisonStart, comparisonEndExclusive),
                InsightLevel.Info,
                hasSparseData));
        }

        var summary = hasSparseData
            ? "Insights are available, but some comparisons are conservative because recent or prior-period activity is limited."
            : $"Insights compare the selected period with the immediately preceding period of the same length. {insights.Count} notable change{(insights.Count == 1 ? "" : "s")} found.";

        return new InsightsResponseDto(
            start,
            endExclusive.AddDays(-1),
            comparisonStart,
            comparisonEndExclusive.AddDays(-1),
            hasSparseData,
            summary,
            insights.Take(5).ToList());
    }

    private async Task AddNetWorthInsightAsync(List<InsightItemDto> insights, Guid userId, InsightsQuery query, DateTime start, DateTime endExclusive, CancellationToken cancellationToken)
    {
        var netWorth = await reportService.GetNetWorthAsync(
            userId,
            new ReportNetWorthQuery(start, endExclusive.AddDays(-1), query.Bucket, query.AccountIds, query.AccountId),
            cancellationToken);

        var change = netWorth.ChangeAmount;
        if (Math.Abs(change) < MaterialNetWorthAmount)
        {
            return;
        }

        insights.Add(new InsightItemDto(
            "net-worth-change",
            change >= 0m ? "Net worth increased" : "Net worth decreased",
            change >= 0m
                ? $"Net worth increased by {RoundMoney(change):0.00} over the selected period."
                : $"Net worth decreased by {RoundMoney(Math.Abs(change)):0.00} over the selected period.",
            $"Based on the net worth trend from {start:dd MMM yyyy} to {endExclusive.AddDays(-1):dd MMM yyyy}.",
            change >= 0m ? InsightLevel.Positive : InsightLevel.Attention,
            false));
    }

    private async Task AddBudgetInsightAsync(List<InsightItemDto> insights, Guid userId, bool includesSharedGuestAccounts, IReadOnlyCollection<Transaction> currentTransactions, IReadOnlyCollection<Transaction> comparisonTransactions, DateTime start, DateTime endExclusive, DateTime comparisonStart, DateTime comparisonEndExclusive, CancellationToken cancellationToken)
    {
        if (includesSharedGuestAccounts)
        {
            return;
        }

        var currentUtilization = await CalculateBudgetUtilizationAsync(userId, currentTransactions, start, endExclusive, cancellationToken);
        var previousUtilization = await CalculateBudgetUtilizationAsync(userId, comparisonTransactions, comparisonStart, comparisonEndExclusive, cancellationToken);
        if (!currentUtilization.HasValue || !previousUtilization.HasValue)
        {
            return;
        }

        var delta = RoundMoney(currentUtilization.Value - previousUtilization.Value);
        if (Math.Abs(delta) < MaterialBudgetChange)
        {
            return;
        }

        var improved = delta < 0m;
        insights.Add(new InsightItemDto(
            "budget-adherence",
            improved ? "Budget adherence improved" : "Budget adherence softened",
            improved
                ? $"Budget use is {Math.Abs(delta):0.0} percentage points tighter than the prior period."
                : $"Budget use is {Math.Abs(delta):0.0} percentage points looser than the prior period.",
            BuildBasis(start, endExclusive, comparisonStart, comparisonEndExclusive),
            improved ? InsightLevel.Positive : InsightLevel.Attention,
            false));
    }

    private void AddIncomeInsight(List<InsightItemDto> insights, IReadOnlyCollection<Transaction> currentTransactions, IReadOnlyCollection<Transaction> comparisonTransactions, DateTime start, DateTime endExclusive, DateTime comparisonStart, DateTime comparisonEndExclusive)
    {
        var currentIncome = currentTransactions.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount);
        var previousIncome = comparisonTransactions.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount);
        if (previousIncome <= 0m)
        {
            return;
        }

        var delta = currentIncome - previousIncome;
        var percentChange = RoundMoney((delta / previousIncome) * 100m);
        if (Math.Abs(percentChange) < MaterialPercentChange || Math.Abs(delta) < MaterialIncomeAmount)
        {
            return;
        }

        insights.Add(new InsightItemDto(
            "income-change",
            delta >= 0m ? "Income increased" : "Income dropped",
            delta >= 0m
                ? $"Income is up {Math.Abs(percentChange):0.0}% compared with the prior period."
                : $"Income is down {Math.Abs(percentChange):0.0}% compared with the prior period.",
            BuildBasis(start, endExclusive, comparisonStart, comparisonEndExclusive),
            delta >= 0m ? InsightLevel.Positive : InsightLevel.Attention,
            false));
    }

    private void AddSavingsRateInsight(List<InsightItemDto> insights, IReadOnlyCollection<Transaction> currentTransactions, IReadOnlyCollection<Transaction> comparisonTransactions, DateTime start, DateTime endExclusive, DateTime comparisonStart, DateTime comparisonEndExclusive)
    {
        var currentSavingsRate = CalculateSavingsRate(currentTransactions);
        var previousSavingsRate = CalculateSavingsRate(comparisonTransactions);
        if (!currentSavingsRate.HasValue || !previousSavingsRate.HasValue)
        {
            return;
        }

        var delta = RoundMoney(currentSavingsRate.Value - previousSavingsRate.Value);
        if (Math.Abs(delta) < MaterialSavingsRateChange)
        {
            return;
        }

        insights.Add(new InsightItemDto(
            "savings-rate-change",
            delta >= 0m ? "Savings rate improved" : "Savings rate slipped",
            delta >= 0m
                ? $"You saved {Math.Abs(delta):0.0} percentage points more income than in the prior period."
                : $"You saved {Math.Abs(delta):0.0} percentage points less income than in the prior period.",
            BuildBasis(start, endExclusive, comparisonStart, comparisonEndExclusive),
            delta >= 0m ? InsightLevel.Positive : InsightLevel.Attention,
            false));
    }

    private static void AddCategorySpendingInsight(List<InsightItemDto> insights, IReadOnlyCollection<Transaction> currentTransactions, IReadOnlyCollection<Transaction> comparisonTransactions, HashSet<Guid> categoryFilterIds, DateTime start, DateTime endExclusive, DateTime comparisonStart, DateTime comparisonEndExclusive)
    {
        var currentCategorySpend = currentTransactions
            .Where(x => x.Type == TransactionType.Expense && x.CategoryId.HasValue && x.Category is not null)
            .Where(x => categoryFilterIds.Count == 0 || categoryFilterIds.Contains(x.CategoryId!.Value))
            .GroupBy(x => new { CategoryId = x.CategoryId!.Value, x.Category!.Name })
            .Select(g => new { g.Key.CategoryId, g.Key.Name, Amount = g.Sum(x => x.Amount) })
            .ToDictionary(x => x.CategoryId, x => (x.Name, x.Amount));

        var previousCategorySpend = comparisonTransactions
            .Where(x => x.Type == TransactionType.Expense && x.CategoryId.HasValue)
            .Where(x => categoryFilterIds.Count == 0 || categoryFilterIds.Contains(x.CategoryId!.Value))
            .GroupBy(x => x.CategoryId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var candidate = currentCategorySpend
            .Select(current =>
            {
                var previousAmount = previousCategorySpend.GetValueOrDefault(current.Key);
                var delta = current.Value.Amount - previousAmount;
                var percentChange = previousAmount > 0m ? RoundMoney((delta / previousAmount) * 100m) : (current.Value.Amount > 0m ? 100m : 0m);
                return new
                {
                    current.Value.Name,
                    Current = current.Value.Amount,
                    Previous = previousAmount,
                    Delta = delta,
                    PercentChange = percentChange
                };
            })
            .Where(x => Math.Abs(x.Delta) >= MaterialCategoryAmount && (x.Previous > 0m || x.Current > 0m))
            .OrderByDescending(x => Math.Abs(x.PercentChange))
            .ThenByDescending(x => Math.Abs(x.Delta))
            .FirstOrDefault();

        if (candidate is null || (candidate.Previous > 0m && Math.Abs(candidate.PercentChange) < MaterialPercentChange))
        {
            return;
        }

        insights.Add(new InsightItemDto(
            "category-spend-change",
            candidate.Delta >= 0m ? $"{candidate.Name} spending increased" : $"{candidate.Name} spending eased",
            candidate.Delta >= 0m
                ? $"Your {candidate.Name.ToLowerInvariant()} spending increased {Math.Abs(candidate.PercentChange):0.0}% compared with the prior period."
                : $"Your {candidate.Name.ToLowerInvariant()} spending decreased {Math.Abs(candidate.PercentChange):0.0}% compared with the prior period.",
            BuildBasis(start, endExclusive, comparisonStart, comparisonEndExclusive),
            candidate.Delta >= 0m ? InsightLevel.Attention : InsightLevel.Positive,
            false));
    }

    private async Task<decimal?> CalculateBudgetUtilizationAsync(Guid userId, IReadOnlyCollection<Transaction> transactions, DateTime start, DateTime endExclusive, CancellationToken cancellationToken)
    {
        var months = EnumerateMonths(start, endExclusive).ToList();
        if (months.Count == 0)
        {
            return null;
        }
        var monthKeys = months.Select(month => (month.Year * 100) + month.Month).ToHashSet();
        var budgets = await dbContext.Budgets
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Where(x => monthKeys.Contains((x.Year * 100) + x.Month))
            .ToListAsync(cancellationToken);

        if (budgets.Count == 0)
        {
            return null;
        }

        var budgetByMonthCategory = budgets
            .GroupBy(x => (x.Year, x.Month, x.CategoryId))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var actualByMonthCategory = transactions
            .Where(x => x.Type == TransactionType.Expense && x.CategoryId.HasValue)
            .GroupBy(x => (x.DateUtc.Year, x.DateUtc.Month, CategoryId: x.CategoryId!.Value))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var totalBudget = budgetByMonthCategory.Values.Sum();
        if (totalBudget <= 0m)
        {
            return null;
        }

        var budgetedActual = budgetByMonthCategory.Keys.Sum(key => actualByMonthCategory.GetValueOrDefault(key));
        return RoundMoney((budgetedActual / totalBudget) * 100m);
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

    private static IReadOnlyCollection<Guid> ResolveRequestedIds(Guid[]? many, Guid? single)
    {
        if (many is { Length: > 0 })
        {
            return many.Distinct().ToArray();
        }

        return single.HasValue ? [single.Value] : [];
    }

    private static decimal? CalculateSavingsRate(IReadOnlyCollection<Transaction> transactions)
    {
        var income = transactions.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount);
        var expense = transactions.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount);
        if (income <= 0m)
        {
            return null;
        }

        return RoundMoney(((income - expense) / income) * 100m);
    }

    private static IEnumerable<DateTime> EnumerateMonths(DateTime start, DateTime endExclusive)
    {
        var current = new DateTime(start.Year, start.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        while (current < endExclusive)
        {
            yield return current;
            current = current.AddMonths(1);
        }
    }

    private static string BuildBasis(DateTime start, DateTime endExclusive, DateTime comparisonStart, DateTime comparisonEndExclusive)
        => $"Compared {start:dd MMM yyyy} to {endExclusive.AddDays(-1):dd MMM yyyy} against {comparisonStart:dd MMM yyyy} to {comparisonEndExclusive.AddDays(-1):dd MMM yyyy}.";

    private static DateTime NormalizeDate(DateTime value)
        => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    private static decimal RoundMoney(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}

