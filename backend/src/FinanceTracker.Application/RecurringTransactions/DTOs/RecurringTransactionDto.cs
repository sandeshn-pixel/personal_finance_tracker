using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.RecurringTransactions.DTOs;

public sealed record RecurringTransactionDto(
    Guid Id,
    string Title,
    TransactionType Type,
    decimal Amount,
    Guid AccountId,
    string AccountName,
    Guid? TransferAccountId,
    string? TransferAccountName,
    Guid? CategoryId,
    string? CategoryName,
    RecurringFrequency Frequency,
    DateTime StartDateUtc,
    DateTime? EndDateUtc,
    DateTime? NextRunDateUtc,
    bool AutoCreateTransaction,
    RecurringRuleStatus Status,
    bool CanManage,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    DateTime? LastProcessedAtUtc);
