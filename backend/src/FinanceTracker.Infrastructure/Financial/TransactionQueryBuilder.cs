using FinanceTracker.Application.Transactions.DTOs;
using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Infrastructure.Financial;

internal static class TransactionQueryBuilder
{
    public static IQueryable<Transaction> ApplyFilters(this IQueryable<Transaction> transactions, Guid userId, TransactionListQuery query)
    {
        transactions = transactions.WhereUserCanView(userId);

        if (query.StartDateUtc.HasValue)
        {
            transactions = transactions.Where(x => x.DateUtc >= query.StartDateUtc.Value);
        }

        if (query.EndDateUtc.HasValue)
        {
            transactions = transactions.Where(x => x.DateUtc <= query.EndDateUtc.Value);
        }

        if (query.CategoryId.HasValue)
        {
            transactions = transactions.Where(x => x.CategoryId == query.CategoryId.Value);
        }

        var requestedAccountIds = ResolveRequestedAccountIds(query);
        if (requestedAccountIds.Count > 0)
        {
            transactions = transactions.Where(x => requestedAccountIds.Contains(x.AccountId) || (x.TransferAccountId.HasValue && requestedAccountIds.Contains(x.TransferAccountId.Value)));
        }

        if (query.Type.HasValue)
        {
            transactions = transactions.Where(x => x.Type == query.Type.Value);
        }

        if (query.MinAmount.HasValue)
        {
            transactions = transactions.Where(x => x.Amount >= query.MinAmount.Value);
        }

        if (query.MaxAmount.HasValue)
        {
            transactions = transactions.Where(x => x.Amount <= query.MaxAmount.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            transactions = transactions.Where(x =>
                (x.Note ?? string.Empty).ToLower().Contains(term)
                || (x.Merchant ?? string.Empty).ToLower().Contains(term));
        }

        return transactions;
    }

    private static HashSet<Guid> ResolveRequestedAccountIds(TransactionListQuery query)
    {
        if (query.AccountIds is { Length: > 0 })
        {
            return query.AccountIds.ToHashSet();
        }

        return query.AccountId.HasValue ? [query.AccountId.Value] : [];
    }
}
