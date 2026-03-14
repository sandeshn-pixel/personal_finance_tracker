using System.Data;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions.DTOs;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FinanceTracker.Infrastructure.Financial;

internal static class TransactionMapping
{
    public static IQueryable<TransactionDto> ProjectToDto(this IQueryable<Transaction> query)
    {
        return query.Select(x => new TransactionDto(
            x.Id,
            x.AccountId,
            x.Account.Name,
            x.TransferAccountId,
            x.TransferAccount != null ? x.TransferAccount.Name : null,
            x.Type,
            x.Amount,
            x.DateUtc,
            x.CategoryId,
            x.Category != null ? x.Category.Name : null,
            x.Note,
            x.Merchant,
            x.PaymentMethod,
            x.RecurringTransactionId,
            x.Tags.OrderBy(t => t.Value).Select(t => t.Value).ToList(),
            x.CreatedUtc,
            x.UpdatedUtc));
    }

    public static IReadOnlyCollection<string> NormalizeTags(IEnumerable<string>? tags)
    {
        return (tags ?? [])
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static void ApplyImpact(TransactionType type, decimal amount, Account sourceAccount, Account? transferAccount)
    {
        switch (type)
        {
            case TransactionType.Income:
                sourceAccount.CurrentBalance += amount;
                break;
            case TransactionType.Expense:
                sourceAccount.CurrentBalance -= amount;
                break;
            case TransactionType.Transfer:
                if (transferAccount is null)
                {
                    throw new ValidationException("Transfer transactions require a destination account.");
                }
                sourceAccount.CurrentBalance -= amount;
                transferAccount.CurrentBalance += amount;
                break;
            default:
                throw new ValidationException("Unsupported transaction type.");
        }
    }

    public static void ReverseImpact(Transaction transaction, Account sourceAccount, Account? transferAccount)
    {
        switch (transaction.Type)
        {
            case TransactionType.Income:
                sourceAccount.CurrentBalance -= transaction.Amount;
                break;
            case TransactionType.Expense:
                sourceAccount.CurrentBalance += transaction.Amount;
                break;
            case TransactionType.Transfer:
                if (transferAccount is null)
                {
                    throw new ValidationException("Transfer transactions require a destination account.");
                }
                sourceAccount.CurrentBalance += transaction.Amount;
                transferAccount.CurrentBalance -= transaction.Amount;
                break;
        }
    }

    public static Task<IDbContextTransaction> BeginFinancialTransactionAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
        => dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
}
