using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Entities;

public sealed class RefreshToken : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public DateTime ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? RevocationReason { get; set; }
    public string? DeviceName { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public User User { get; set; } = null!;

    public bool IsActive => RevokedUtc is null && ExpiresUtc > DateTime.UtcNow;
}
