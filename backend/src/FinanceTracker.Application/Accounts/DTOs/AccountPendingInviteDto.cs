using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Accounts.DTOs;

public sealed record AccountPendingInviteDto(
    Guid Id,
    string Email,
    AccountMemberRole Role,
    string InvitedByDisplayName,
    DateTime CreatedUtc,
    DateTime ExpiresUtc,
    bool IsExpired);
