using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Domain.Entities;

public sealed class UserNotification : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public NotificationLevel Level { get; set; } = NotificationLevel.Info;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Route { get; set; }
    public string? DeduplicationKey { get; set; }
    public DateTime? ReadAtUtc { get; set; }

    public User User { get; set; } = null!;
}