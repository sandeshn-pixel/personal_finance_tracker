using FinanceTracker.Application.Transactions.DTOs;
using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Infrastructure.Financial;

internal static class TransactionQueryBuilder
{
    public static IQueryable<Transaction> ApplyFilters(this IQueryable<Transaction> transactions, Guid userId, TransactionListQuery query)
    {
        transactions = transactions.Where(x => x.UserId == userId && !x.IsDeleted);

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

        if (query.AccountId.HasValue)
        {
            transactions = transactions.Where(x => x.AccountId == query.AccountId.Value || x.TransferAccountId == query.AccountId.Value);
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
}