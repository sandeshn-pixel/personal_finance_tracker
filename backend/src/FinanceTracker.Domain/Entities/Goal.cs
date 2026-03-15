using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Domain.Entities;

public sealed class Goal : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public DateTime? TargetDateUtc { get; set; }
    public Guid? LinkedAccountId { get; set; }
    public string? Icon { get; set; }
    public string? Color { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.Active;

    public User User { get; set; } = null!;
    public Account? LinkedAccount { get; set; }
    public ICollection<GoalEntry> Entries { get; set; } = new List<GoalEntry>();
}