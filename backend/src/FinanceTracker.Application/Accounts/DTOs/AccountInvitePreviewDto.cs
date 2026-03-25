using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Accounts.DTOs;

public sealed record AccountInvitePreviewDto(
    Guid InviteId,
    Guid AccountId,
    string AccountName,
    string OwnerDisplayName,
    string Email,
    AccountMemberRole Role,
    string Status,
    DateTime ExpiresUtc,
    bool CanAccept,
    bool RequiresDifferentAccount,
    string StatusMessage);
