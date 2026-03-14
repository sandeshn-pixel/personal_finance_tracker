using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Transactions.DTOs;

public sealed class UpsertTransactionRequest
{
    public Guid AccountId { get; init; }
    public Guid? TransferAccountId { get; init; }
    public TransactionType Type { get; init; }
    public decimal Amount { get; init; }
    public DateTime DateUtc { get; init; }
    public Guid? CategoryId { get; init; }
    public string? Note { get; init; }
    public string? Merchant { get; init; }
    public string? PaymentMethod { get; init; }
    public Guid? RecurringTransactionId { get; init; }
    public IReadOnlyCollection<string> Tags { get; init; } = [];
}
