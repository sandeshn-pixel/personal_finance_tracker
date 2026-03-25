namespace FinanceTracker.Application.Accounts.DTOs;

public sealed record InviteAccountMemberResponse(
    AccountPendingInviteDto Invite,
    string? PreviewUrl);
