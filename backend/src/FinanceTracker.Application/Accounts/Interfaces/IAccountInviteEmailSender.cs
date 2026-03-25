namespace FinanceTracker.Application.Accounts.Interfaces;

public interface IAccountInviteEmailSender
{
    Task SendInviteAsync(string email, string ownerDisplayName, string accountName, string inviteUrl, CancellationToken cancellationToken);
}
