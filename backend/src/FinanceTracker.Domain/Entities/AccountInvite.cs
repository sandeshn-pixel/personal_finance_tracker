using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Domain.Entities;

public sealed class AccountInvite : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string Email { get; set; } = string.Empty;
    public AccountMemberRole Role { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresUtc { get; set; }
    public AccountInviteStatus Status { get; set; } = AccountInviteStatus.Pending;
    public Guid InvitedByUserId { get; set; }
    public Guid? AcceptedByUserId { get; set; }
    public Guid? RevokedByUserId { get; set; }
    public DateTime? AcceptedUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }

    public Account Account { get; set; } = null!;
    public User InvitedByUser { get; set; } = null!;
    public User? AcceptedByUser { get; set; }
    public User? RevokedByUser { get; set; }
}
