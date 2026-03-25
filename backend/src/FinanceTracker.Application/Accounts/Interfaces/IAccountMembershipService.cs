using FinanceTracker.Application.Accounts.DTOs;

namespace FinanceTracker.Application.Accounts.Interfaces;

public interface IAccountMembershipService
{
    Task<IReadOnlyCollection<AccountMemberDto>> ListAsync(Guid userId, Guid accountId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AccountPendingInviteDto>> ListPendingInvitesAsync(Guid userId, Guid accountId, CancellationToken cancellationToken);
    Task<InviteAccountMemberResponse> InviteAsync(Guid userId, Guid accountId, InviteAccountMemberRequest request, CancellationToken cancellationToken);
    Task<InviteAccountMemberResponse> ResendInviteAsync(Guid userId, Guid accountId, Guid inviteId, CancellationToken cancellationToken);
    Task<AccountInvitePreviewDto> PreviewInviteAsync(Guid? userId, string token, CancellationToken cancellationToken);
    Task<AccountMemberDto> AcceptInviteAsync(Guid userId, AcceptAccountInviteRequest request, CancellationToken cancellationToken);
    Task<AccountMemberDto> UpdateAsync(Guid userId, Guid accountId, Guid memberUserId, UpdateAccountMemberRequest request, CancellationToken cancellationToken);
    Task RemoveAsync(Guid userId, Guid accountId, Guid memberUserId, CancellationToken cancellationToken);
    Task RevokeInviteAsync(Guid userId, Guid accountId, Guid inviteId, CancellationToken cancellationToken);
}
