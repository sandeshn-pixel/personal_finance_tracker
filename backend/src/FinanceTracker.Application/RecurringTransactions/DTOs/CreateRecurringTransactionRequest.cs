using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.RecurringTransactions.DTOs;

public sealed class CreateRecurringTransactionRequest
{
    public string Title { get; init; } = string.Empty;
    public TransactionType Type { get; init; }
    public decimal Amount { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid AccountId { get; init; }
    public Guid? TransferAccountId { get; init; }
    public RecurringFrequency Frequency { get; init; }
    public DateTime StartDateUtc { get; init; }
    public DateTime? EndDateUtc { get; init; }
    public bool AutoCreateTransaction { get; init; } = true;
}