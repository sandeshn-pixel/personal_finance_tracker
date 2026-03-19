using FinanceTracker.Application.Common;
using FinanceTracker.Application.Reports.DTOs;
using FinanceTracker.Application.Reports.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Reporting;

public sealed class ReportService(ApplicationDbContext dbContext) : IReportService
{
    public async Task<ReportsOverviewDto> GetOverviewAsync(Guid userId, ReportQuery query, CancellationToken cancellationToken)
    {
        var start = DateTime.SpecifyKind(query.StartDateUtc.Date, DateTimeKind.Utc);
        var endExclusive = DateTime.SpecifyKind(query.EndDateUtc.Date.AddDays(1), DateTimeKind.Utc);
        var periodLength = endExclusive - start;
        var previousStart = start - periodLength;

        var selectedAccounts = await LoadSelectedAccountsAsync(userId, query.AccountId, cancellationToken);
        var selectedAccountIds = selectedAccounts.Select(x => x.Id).ToHashSet();

        var reportTransactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted && x.DateUtc >= start && x.DateUtc < endExclusive)
            .Where(x => !query.AccountId.HasValue || x.AccountId == query.AccountId.Value || x.TransferAccountId == query.AccountId.Value)
            .Include(x => x.Category)
            .ToListAsync(cancellationToken);

        var previousTransactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted && x.DateUtc >= previousStart && x.DateUtc < start)
            .Where(x => !query.AccountId.HasValue || x.AccountId == query.AccountId.Value || x.TransferAccountId == query.AccountId.Value)
            .ToListAsync(cancellationToken);

        var summary = BuildSummary(reportTransactions);
        var comparison = BuildComparison(previousTransactions);

        var categorySpend = reportTransactions
            .Where(x => x.Type == TransactionType.Expense && x.CategoryId.HasValue && x.Category is not null)
            .GroupBy(x => new { x.CategoryId, x.Category!.Name })
            .Select(g => new CategorySpendReportItemDto(g.Key.CategoryId!.Value, g.Key.Name, g.Sum(x => x.Amount)))
            .OrderByDescending(x => x.Amount)
            .ToList();

        var topMerchants = reportTransactions
            .Where(x => x.Type == TransactionType.Expense && !string.IsNullOrWhiteSpace(x.Merchant))
            .GroupBy(x => x.Merchant!.Trim())
            .Select(g => new MerchantSpendReportItemDto(g.Key, g.Sum(x => x.Amount), g.Count()))
            .OrderByDescending(x => x.Amount)
            .ThenBy(x => x.MerchantName)
            .Take(5)
            .ToList();

        var bucketMode = ResolveBucketMode(start, endExclusive);
        var buckets = CreateBuckets(start, endExclusive, bucketMode);

        var incomeExpenseTrend = buckets
            .Select(bucket =>
            {
                var bucketEnd = NextBucket(bucket, bucketMode);
                var bucketTransactions = reportTransactions.Where(x => x.DateUtc >= bucket && x.DateUtc < bucketEnd);
                return new IncomeExpenseTrendPointDto(
                    bucket,
                    FormatBucket(bucket, bucketMode),
                    bucketTransactions.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount),
                    bucketTransactions.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount));
            })
            .ToList();

        var preRangeTransactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted && x.DateUtc < start)
            .Where(x => !query.AccountId.HasValue || x.AccountId == query.AccountId.Value || x.TransferAccountId == query.AccountId.Value)
            .ToListAsync(cancellationToken);

        var startingBalance = selectedAccounts.Sum(x => x.OpeningBalance) + preRangeTransactions.Sum(x => CalculateImpact(x, selectedAccountIds));
        var runningBalance = startingBalance;

        var accountBalanceTrend = new List<AccountBalanceTrendPointDto>(buckets.Count);
        foreach (var bucket in buckets)
        {
            var bucketEnd = NextBucket(bucket, bucketMode);
            runningBalance += reportTransactions
                .Where(x => x.DateUtc >= bucket && x.DateUtc < bucketEnd)
                .Sum(x => CalculateImpact(x, selectedAccountIds));

            accountBalanceTrend.Add(new AccountBalanceTrendPointDto(bucket, FormatBucket(bucket, bucketMode), runningBalance));
        }

        return new ReportsOverviewDto(summary, comparison, categorySpend, topMerchants, incomeExpenseTrend, accountBalanceTrend);
    }

    private static ReportSummaryDto BuildSummary(IEnumerable<Transaction> transactions)
    {
        var source = transactions.ToList();
        var totalIncome = source.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount);
        var totalExpense = source.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount);

        return new ReportSummaryDto(
            totalIncome,
            totalExpense,
            totalIncome - totalExpense,
            source.Count(x => x.Type == TransactionType.Expense),
            source.Count(x => x.Type == TransactionType.Income));
    }

    private static ReportPeriodComparisonDto BuildComparison(IEnumerable<Transaction> previousTransactions)
    {
        var source = previousTransactions.ToList();
        var previousTotalIncome = source.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount);
        var previousTotalExpense = source.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount);

        return new ReportPeriodComparisonDto(
            previousTotalIncome,
            previousTotalExpense,
            previousTotalIncome - previousTotalExpense,
            source.Count(x => x.Type == TransactionType.Expense),
            source.Count(x => x.Type == TransactionType.Income));
    }

    private async Task<List<Account>> LoadSelectedAccountsAsync(Guid userId, Guid? accountId, CancellationToken cancellationToken)
    {
        if (!accountId.HasValue)
        {
            return await dbContext.Accounts
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .ToListAsync(cancellationToken);
        }

        var account = await dbContext.Accounts
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == accountId.Value, cancellationToken)
            ?? throw new ValidationException("Selected account was not found.");

        return [account];
    }

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

    private static BucketMode ResolveBucketMode(DateTime start, DateTime endExclusive)
        => (endExclusive.Date - start.Date).TotalDays <= 90 ? BucketMode.Week : BucketMode.Month;

    private static List<DateTime> CreateBuckets(DateTime start, DateTime endExclusive, BucketMode mode)
    {
        var buckets = new List<DateTime>();
        var current = mode switch
        {
            BucketMode.Week => StartOfWeek(start),
            _ => new DateTime(start.Year, start.Month, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        while (current < endExclusive)
        {
            buckets.Add(current);
            current = NextBucket(current, mode);
        }

        return buckets;
    }

    private static DateTime NextBucket(DateTime value, BucketMode mode)
        => mode == BucketMode.Week ? value.AddDays(7) : value.AddMonths(1);

    private static string FormatBucket(DateTime value, BucketMode mode)
        => mode == BucketMode.Week ? $"Week of {value:dd MMM}" : value.ToString("MMM yyyy");

    private static DateTime StartOfWeek(DateTime value)
    {
        var normalized = DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
        var diff = ((int)normalized.DayOfWeek + 6) % 7;
        return normalized.AddDays(-diff);
    }

    private enum BucketMode
    {
        Week,
        Month
    }
}
