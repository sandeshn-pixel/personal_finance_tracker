using FinanceTracker.Application.Common;
using FinanceTracker.Application.Reports.DTOs;
using FinanceTracker.Application.Reports.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Financial;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Reporting;

public sealed class ReportService(
    ApplicationDbContext dbContext,
    AccountAccessService accountAccessService) : IReportService
{
    private const int CategoryTrendSeriesLimit = 5;

    public async Task<ReportsOverviewDto> GetOverviewAsync(Guid userId, ReportQuery query, CancellationToken cancellationToken)
    {
        var start = NormalizeDate(query.StartDateUtc);
        var endExclusive = NormalizeDate(query.EndDateUtc).AddDays(1);
        var periodLength = endExclusive - start;
        var previousStart = start - periodLength;

        var selectedAccounts = await LoadSelectedAccountsAsync(userId, ResolveRequestedAccountIds(query.AccountIds, query.AccountId), cancellationToken);
        var selectedAccountIds = selectedAccounts.Select(x => x.Id).ToHashSet();
        var transactions = await LoadTransactionsAsync(userId, start, endExclusive, selectedAccountIds, includeCategories: true, cancellationToken);
        var previousTransactions = await LoadTransactionsAsync(userId, previousStart, start, selectedAccountIds, includeCategories: false, cancellationToken);

        var summary = BuildSummary(transactions);
        var comparison = BuildComparison(previousTransactions);

        var categorySpend = transactions
            .Where(x => x.Type == TransactionType.Expense && x.CategoryId.HasValue && x.Category is not null)
            .GroupBy(x => new { x.CategoryId, x.Category!.Name })
            .Select(g => new CategorySpendReportItemDto(g.Key.CategoryId!.Value, g.Key.Name, RoundMoney(g.Sum(x => x.Amount))))
            .OrderByDescending(x => x.Amount)
            .ToList();

        var topMerchants = transactions
            .Where(x => x.Type == TransactionType.Expense && !string.IsNullOrWhiteSpace(x.Merchant))
            .GroupBy(x => x.Merchant!.Trim())
            .Select(g => new MerchantSpendReportItemDto(g.Key, RoundMoney(g.Sum(x => x.Amount)), g.Count()))
            .OrderByDescending(x => x.Amount)
            .ThenBy(x => x.MerchantName)
            .Take(5)
            .ToList();

        var bucket = ResolveBucket(start, endExclusive, ReportTimeBucket.Auto);
        var buckets = CreateBuckets(start, endExclusive, bucket);

        var incomeExpenseTrend = buckets
            .Select(bucketStart =>
            {
                var bucketEnd = NextBucket(bucketStart, bucket);
                var bucketTransactions = transactions.Where(x => x.DateUtc >= bucketStart && x.DateUtc < bucketEnd).ToList();
                return new IncomeExpenseTrendPointDto(
                    bucketStart,
                    FormatBucket(bucketStart, bucket),
                    RoundMoney(bucketTransactions.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount)),
                    RoundMoney(bucketTransactions.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount)));
            })
            .ToList();

        var preRangeTransactions = await dbContext.Transactions
            .AsNoTracking()
            .WhereUserCanView(userId)
            .Where(x => x.DateUtc < start)
            .Where(x => selectedAccountIds.Contains(x.AccountId) || (x.TransferAccountId.HasValue && selectedAccountIds.Contains(x.TransferAccountId.Value)))
            .ToListAsync(cancellationToken);

        var startingBalance = RoundMoney(selectedAccounts.Sum(x => x.OpeningBalance) + preRangeTransactions.Sum(x => CalculateImpact(x, selectedAccountIds)));
        var runningBalance = startingBalance;
        var accountBalanceTrend = new List<AccountBalanceTrendPointDto>(buckets.Count);

        foreach (var bucketStart in buckets)
        {
            var bucketEnd = NextBucket(bucketStart, bucket);
            runningBalance = RoundMoney(runningBalance + transactions
                .Where(x => x.DateUtc >= bucketStart && x.DateUtc < bucketEnd)
                .Sum(x => CalculateImpact(x, selectedAccountIds)));

            accountBalanceTrend.Add(new AccountBalanceTrendPointDto(bucketStart, FormatBucket(bucketStart, bucket), runningBalance));
        }

        return new ReportsOverviewDto(summary, comparison, categorySpend, topMerchants, incomeExpenseTrend, accountBalanceTrend);
    }

    public async Task<ReportsTrendResponseDto> GetTrendsAsync(Guid userId, ReportTrendsQuery query, CancellationToken cancellationToken)
    {
        var start = NormalizeDate(query.StartDateUtc);
        var endExclusive = NormalizeDate(query.EndDateUtc).AddDays(1);
        var selectedAccounts = await LoadSelectedAccountsAsync(userId, ResolveRequestedAccountIds(query.AccountIds, query.AccountId), cancellationToken);
        var selectedAccountIds = selectedAccounts.Select(x => x.Id).ToHashSet();
        var bucket = ResolveBucket(start, endExclusive, query.Bucket);
        var buckets = CreateBuckets(start, endExclusive, bucket);
        var categoryFilterIds = ResolveRequestedAccountIds(query.CategoryIds, query.CategoryId).ToHashSet();

        var transactions = await LoadTransactionsAsync(userId, start, endExclusive, selectedAccountIds, includeCategories: true, cancellationToken);
        var expenseTransactions = transactions.Where(x => x.Type == TransactionType.Expense && x.CategoryId.HasValue && x.Category is not null).ToList();
        var filteredExpenseTransactions = categoryFilterIds.Count == 0
            ? expenseTransactions
            : expenseTransactions.Where(x => x.CategoryId.HasValue && categoryFilterIds.Contains(x.CategoryId.Value)).ToList();

        var incomeExpenseTrend = buckets
            .Select(bucketStart =>
            {
                var bucketEnd = NextBucket(bucketStart, bucket);
                var bucketTransactions = transactions.Where(x => x.DateUtc >= bucketStart && x.DateUtc < bucketEnd).ToList();
                return new IncomeExpenseTrendPointDto(
                    bucketStart,
                    FormatBucket(bucketStart, bucket),
                    RoundMoney(bucketTransactions.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount)),
                    RoundMoney(bucketTransactions.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount)));
            })
            .ToList();

        var savingsRateTrend = incomeExpenseTrend
            .Select(point =>
            {
                var income = point.Income;
                var expense = point.Expense;
                var netSavings = RoundMoney(income - expense);
                var hasIncomeData = income > 0m;
                decimal? savingsRatePercent = hasIncomeData
                    ? RoundMoney((netSavings / income) * 100m)
                    : null;
                return new SavingsRateTrendPointDto(point.PeriodStartUtc, point.Label, income, expense, netSavings, savingsRatePercent, hasIncomeData);
            })
            .ToList();

        var categorySeries = filteredExpenseTransactions
            .GroupBy(x => new { x.CategoryId, x.Category!.Name })
            .Select(g => new
            {
                CategoryId = g.Key.CategoryId!.Value,
                g.Key.Name,
                TotalAmount = RoundMoney(g.Sum(x => x.Amount))
            })
            .OrderByDescending(x => x.TotalAmount)
            .Take(categoryFilterIds.Count > 0 ? categoryFilterIds.Count : CategoryTrendSeriesLimit)
            .ToList()
            .Select(category => new CategoryTrendSeriesDto(
                category.CategoryId,
                category.Name,
                category.TotalAmount,
                buckets.Select(bucketStart =>
                {
                    var bucketEnd = NextBucket(bucketStart, bucket);
                    var amount = filteredExpenseTransactions
                        .Where(x => x.CategoryId == category.CategoryId && x.DateUtc >= bucketStart && x.DateUtc < bucketEnd)
                        .Sum(x => x.Amount);
                    return new CategoryTrendPointDto(bucketStart, FormatBucket(bucketStart, bucket), RoundMoney(amount));
                }).ToList()))
            .ToList();

        var hasSparseData = transactions.Count < 6 || buckets.Count < 2;
        var basisDescription = categoryFilterIds.Count > 0
            ? "Income, expense, and savings trends use the full selected scope. Category trends are narrowed to the selected category filter."
            : "Income, expense, savings, and category trends are aggregated from persisted transactions in the selected date range. Transfers are excluded from trend math.";

        return new ReportsTrendResponseDto(
            start,
            endExclusive.AddDays(-1),
            bucket,
            hasSparseData,
            basisDescription,
            incomeExpenseTrend,
            savingsRateTrend,
            categorySeries);
    }

    public async Task<NetWorthReportDto> GetNetWorthAsync(Guid userId, ReportNetWorthQuery query, CancellationToken cancellationToken)
    {
        var start = NormalizeDate(query.StartDateUtc);
        var endExclusive = NormalizeDate(query.EndDateUtc).AddDays(1);
        var bucket = ResolveBucket(start, endExclusive, query.Bucket);
        var buckets = CreateBuckets(start, endExclusive, bucket);

        var selectedAccounts = await LoadSelectedAccountsAsync(userId, ResolveRequestedAccountIds(query.AccountIds, query.AccountId), cancellationToken);
        var selectedAccountIds = selectedAccounts.Select(x => x.Id).ToHashSet();
        var transactions = await dbContext.Transactions
            .AsNoTracking()
            .WhereUserCanView(userId)
            .Where(x => x.DateUtc >= start && x.DateUtc < endExclusive)
            .Where(x => selectedAccountIds.Contains(x.AccountId) || (x.TransferAccountId.HasValue && selectedAccountIds.Contains(x.TransferAccountId.Value)))
            .ToListAsync(cancellationToken);

        var preRangeTransactions = await dbContext.Transactions
            .AsNoTracking()
            .WhereUserCanView(userId)
            .Where(x => x.DateUtc < start)
            .Where(x => selectedAccountIds.Contains(x.AccountId) || (x.TransferAccountId.HasValue && selectedAccountIds.Contains(x.TransferAccountId.Value)))
            .ToListAsync(cancellationToken);

        var startingBalances = selectedAccounts.ToDictionary(
            account => account.Id,
            account => RoundMoney(account.OpeningBalance + preRangeTransactions.Where(x => x.AccountId == account.Id || x.TransferAccountId == account.Id).Sum(x => CalculateAccountImpact(x, account.Id))));

        var runningBalances = startingBalances.ToDictionary(entry => entry.Key, entry => entry.Value);
        var points = new List<NetWorthTrendPointDto>(buckets.Count);

        foreach (var bucketStart in buckets)
        {
            var bucketEnd = NextBucket(bucketStart, bucket);
            var bucketTransactions = transactions.Where(x => x.DateUtc >= bucketStart && x.DateUtc < bucketEnd).ToList();

            foreach (var transaction in bucketTransactions)
            {
                runningBalances[transaction.AccountId] = RoundMoney(runningBalances.GetValueOrDefault(transaction.AccountId) + CalculateAccountImpact(transaction, transaction.AccountId));
                if (transaction.TransferAccountId.HasValue && runningBalances.ContainsKey(transaction.TransferAccountId.Value))
                {
                    runningBalances[transaction.TransferAccountId.Value] = RoundMoney(runningBalances.GetValueOrDefault(transaction.TransferAccountId.Value) + CalculateAccountImpact(transaction, transaction.TransferAccountId.Value));
                }
            }

            var assetBalance = RoundMoney(selectedAccounts.Where(x => x.Type != AccountType.CreditCard).Sum(x => runningBalances.GetValueOrDefault(x.Id)));
            var liabilityBalance = RoundMoney(selectedAccounts.Where(x => x.Type == AccountType.CreditCard).Sum(x => runningBalances.GetValueOrDefault(x.Id)));
            var netWorth = RoundMoney(assetBalance + liabilityBalance);
            points.Add(new NetWorthTrendPointDto(bucketStart, FormatBucket(bucketStart, bucket), netWorth, assetBalance, liabilityBalance));
        }

        var startingNetWorth = RoundMoney(startingBalances.Values.Sum());
        var currentNetWorth = points.Count == 0 ? startingNetWorth : points[^1].NetWorth;
        return new NetWorthReportDto(
            start,
            endExclusive.AddDays(-1),
            bucket,
            currentNetWorth,
            startingNetWorth,
            RoundMoney(currentNetWorth - startingNetWorth),
            selectedAccounts.Count,
            selectedAccounts.Count(x => x.Type == AccountType.CreditCard),
            "Net worth uses account opening balances plus persisted transaction history in the selected range. Credit-card balances are included using their stored sign, so negative balances reduce net worth.",
            points);
    }

    private async Task<List<Account>> LoadSelectedAccountsAsync(Guid userId, IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken)
    {
        if (accountIds.Count == 0)
        {
            return await accountAccessService.QueryAccessibleAccounts(userId, AccountMemberRole.Viewer, includeArchived: false)
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);
        }

        var accessibleAccounts = await accountAccessService.QueryAccessibleAccounts(userId, AccountMemberRole.Viewer, includeArchived: false)
            .AsNoTracking()
            .Where(x => accountIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        if (accessibleAccounts.Count != accountIds.Count)
        {
            throw new ValidationException("One or more selected accounts were not found.");
        }

        return accessibleAccounts;
    }

    private async Task<List<Transaction>> LoadTransactionsAsync(Guid userId, DateTime start, DateTime endExclusive, HashSet<Guid> selectedAccountIds, bool includeCategories, CancellationToken cancellationToken)
    {
        var query = dbContext.Transactions
            .AsNoTracking()
            .WhereUserCanView(userId)
            .Where(x => x.DateUtc >= start && x.DateUtc < endExclusive)
            .Where(x => selectedAccountIds.Contains(x.AccountId) || (x.TransferAccountId.HasValue && selectedAccountIds.Contains(x.TransferAccountId.Value)));

        if (includeCategories)
        {
            query = query.Include(x => x.Category);
        }

        return await query.ToListAsync(cancellationToken);
    }

    private static IReadOnlyCollection<Guid> ResolveRequestedAccountIds(Guid[]? many, Guid? single)
    {
        if (many is { Length: > 0 })
        {
            return many.Distinct().ToArray();
        }

        return single.HasValue ? [single.Value] : [];
    }

    private static ReportSummaryDto BuildSummary(IEnumerable<Transaction> transactions)
    {
        var source = transactions.Where(x => x.Type != TransactionType.Transfer).ToList();
        var totalIncome = RoundMoney(source.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount));
        var totalExpense = RoundMoney(source.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount));

        return new ReportSummaryDto(
            totalIncome,
            totalExpense,
            RoundMoney(totalIncome - totalExpense),
            source.Count(x => x.Type == TransactionType.Expense),
            source.Count(x => x.Type == TransactionType.Income));
    }

    private static ReportPeriodComparisonDto BuildComparison(IEnumerable<Transaction> previousTransactions)
    {
        var source = previousTransactions.Where(x => x.Type != TransactionType.Transfer).ToList();
        var previousTotalIncome = RoundMoney(source.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount));
        var previousTotalExpense = RoundMoney(source.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount));

        return new ReportPeriodComparisonDto(
            previousTotalIncome,
            previousTotalExpense,
            RoundMoney(previousTotalIncome - previousTotalExpense),
            source.Count(x => x.Type == TransactionType.Expense),
            source.Count(x => x.Type == TransactionType.Income));
    }

    private static ReportTimeBucket ResolveBucket(DateTime start, DateTime endExclusive, ReportTimeBucket requested)
    {
        if (requested != ReportTimeBucket.Auto)
        {
            return requested;
        }

        return (endExclusive.Date - start.Date).TotalDays <= 90 ? ReportTimeBucket.Week : ReportTimeBucket.Month;
    }

    private static List<DateTime> CreateBuckets(DateTime start, DateTime endExclusive, ReportTimeBucket bucket)
    {
        var buckets = new List<DateTime>();
        var current = bucket switch
        {
            ReportTimeBucket.Week => StartOfWeek(start),
            _ => new DateTime(start.Year, start.Month, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        while (current < endExclusive)
        {
            buckets.Add(current);
            current = NextBucket(current, bucket);
        }

        return buckets;
    }

    private static DateTime NextBucket(DateTime value, ReportTimeBucket bucket)
        => bucket == ReportTimeBucket.Week ? value.AddDays(7) : value.AddMonths(1);

    private static string FormatBucket(DateTime value, ReportTimeBucket bucket)
        => bucket == ReportTimeBucket.Week ? $"Week of {value:dd MMM}" : value.ToString("MMM yyyy");

    private static DateTime StartOfWeek(DateTime value)
    {
        var normalized = NormalizeDate(value);
        var diff = ((int)normalized.DayOfWeek + 6) % 7;
        return normalized.AddDays(-diff);
    }

    private static DateTime NormalizeDate(DateTime value)
        => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    private static decimal CalculateImpact(Transaction transaction, HashSet<Guid> selectedAccountIds)
    {
        var sourceSelected = selectedAccountIds.Contains(transaction.AccountId);
        var transferSelected = transaction.TransferAccountId.HasValue && selectedAccountIds.Contains(transaction.TransferAccountId.Value);

        return transaction.Type switch
        {
            TransactionType.Income when sourceSelected => transaction.Amount,
            TransactionType.Expense when sourceSelected => -transaction.Amount,
            TransactionType.Transfer => (sourceSelected ? -transaction.Amount : 0m) + (transferSelected ? transaction.Amount : 0m),
            _ => 0m
        };
    }

    private static decimal CalculateAccountImpact(Transaction transaction, Guid accountId)
        => transaction.Type switch
        {
            TransactionType.Income when transaction.AccountId == accountId => transaction.Amount,
            TransactionType.Expense when transaction.AccountId == accountId => -transaction.Amount,
            TransactionType.Transfer when transaction.AccountId == accountId => -transaction.Amount,
            TransactionType.Transfer when transaction.TransferAccountId == accountId => transaction.Amount,
            _ => 0m
        };

    private static decimal RoundMoney(decimal amount) => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}

