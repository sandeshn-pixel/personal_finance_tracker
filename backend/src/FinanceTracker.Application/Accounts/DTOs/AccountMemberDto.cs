using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Accounts.DTOs;

public sealed record AccountMemberDto(
    Guid UserId,
    string Email,
    string FullName,
    AccountMemberRole Role,
    bool IsOwner,
    string? InvitedByDisplayName,
    string? LastModifiedByDisplayName,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);
