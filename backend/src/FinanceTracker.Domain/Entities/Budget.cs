using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Entities;

public sealed class Budget : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CategoryId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
    public int AlertThresholdPercent { get; set; } = 80;

    public User User { get; set; } = null!;
    public Category Category { get; set; } = null!;
}
