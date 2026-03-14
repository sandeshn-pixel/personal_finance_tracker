namespace FinanceTracker.Domain.Common;

public abstract class AuditableEntity
{
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
