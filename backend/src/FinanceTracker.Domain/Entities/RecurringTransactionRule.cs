using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Domain.Entities;

public sealed class RecurringTransactionRule : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? TransferAccountId { get; set; }
    public RecurringFrequency Frequency { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public DateTime? NextRunDateUtc { get; set; }
    public bool AutoCreateTransaction { get; set; } = true;
    public RecurringRuleStatus Status { get; set; } = RecurringRuleStatus.Active;

    public User User { get; set; } = null!;
    public Category? Category { get; set; }
    public Account Account { get; set; } = null!;
    public Account? TransferAccount { get; set; }
    public ICollection<RecurringTransactionExecution> Executions { get; set; } = new List<RecurringTransactionExecution>();
}