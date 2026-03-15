using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Domain.Entities;

public sealed class GoalEntry : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GoalId { get; set; }
    public Guid UserId { get; set; }
    public Guid? AccountId { get; set; }
    public GoalEntryType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal GoalAmountAfterEntry { get; set; }
    public string? Note { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    public Goal Goal { get; set; } = null!;
    public User User { get; set; } = null!;
    public Account? Account { get; set; }
}