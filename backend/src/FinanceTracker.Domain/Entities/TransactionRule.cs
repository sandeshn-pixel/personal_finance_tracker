using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Entities;

public sealed class TransactionRule : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string ConditionJson { get; set; } = string.Empty;
    public string ActionJson { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public User User { get; set; } = null!;
}
