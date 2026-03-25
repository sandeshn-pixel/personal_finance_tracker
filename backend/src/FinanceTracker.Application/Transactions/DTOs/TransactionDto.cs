using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Transactions.DTOs;

public sealed record TransactionDto(
    Guid Id,
    Guid AccountId,
    string AccountName,
    Guid? TransferAccountId,
    string? TransferAccountName,
    TransactionType Type,
    decimal Amount,
    DateTime DateUtc,
    Guid? CategoryId,
    string? CategoryName,
    string? Note,
    string? Merchant,
    string? PaymentMethod,
    Guid? RecurringTransactionId,
    IReadOnlyCollection<string> Tags,
    Guid CreatedByUserId,
    string CreatedByDisplayName,
    Guid UpdatedByUserId,
    string UpdatedByDisplayName,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);
