using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Domain.Entities;

public sealed class AccountMembership : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public Guid UserId { get; set; }
    public AccountMemberRole Role { get; set; }
    public Guid InvitedByUserId { get; set; }
    public Guid LastModifiedByUserId { get; set; }

    public Account Account { get; set; } = null!;
    public User User { get; set; } = null!;
    public User InvitedByUser { get; set; } = null!;
    public User LastModifiedByUser { get; set; } = null!;
}
