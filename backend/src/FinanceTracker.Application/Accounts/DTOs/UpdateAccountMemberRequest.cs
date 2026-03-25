using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Accounts.DTOs;

public sealed record UpdateAccountMemberRequest(AccountMemberRole Role);
