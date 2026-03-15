using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Domain.Entities;

public sealed class RecurringTransactionExecution : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecurringTransactionRuleId { get; set; }
    public DateTime ScheduledForDateUtc { get; set; }
    public RecurringExecutionStatus Status { get; set; } = RecurringExecutionStatus.Processing;
    public Guid? TransactionId { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public string? FailureReason { get; set; }

    public RecurringTransactionRule RecurringTransactionRule { get; set; } = null!;
    public Transaction? Transaction { get; set; }
}