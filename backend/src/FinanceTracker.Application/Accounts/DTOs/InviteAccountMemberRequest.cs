using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Accounts.DTOs;

public sealed record InviteAccountMemberRequest(
    string Email,
    AccountMemberRole Role);
