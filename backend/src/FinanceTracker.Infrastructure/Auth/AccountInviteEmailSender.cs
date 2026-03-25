using System.Net;
using System.Net.Mail;
using FinanceTracker.Application.Accounts.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Auth;

public sealed class AccountInviteEmailSender(
    IOptions<EmailOptions> emailOptions,
    ILogger<AccountInviteEmailSender> logger) : IAccountInviteEmailSender
{
    private readonly EmailOptions _emailOptions = emailOptions.Value;

    public async Task SendInviteAsync(string email, string ownerDisplayName, string accountName, string inviteUrl, CancellationToken cancellationToken)
    {
        if (!_emailOptions.Enabled)
        {
            logger.LogInformation("Account invite email delivery is disabled. Invite link for {Email}: {InviteUrl}", email, inviteUrl);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_emailOptions.FromAddress!, _emailOptions.FromName),
            Subject = $"You're invited to share {accountName} on Ledger Nest",
            Body = BuildTextBody(ownerDisplayName, accountName, inviteUrl),
            IsBodyHtml = false,
        };
        message.To.Add(email);

        using var client = new SmtpClient(_emailOptions.SmtpHost!, _emailOptions.Port)
        {
            EnableSsl = _emailOptions.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
        };

        if (!string.IsNullOrWhiteSpace(_emailOptions.Username))
        {
            client.Credentials = new NetworkCredential(_emailOptions.Username, _emailOptions.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }

    private static string BuildTextBody(string ownerDisplayName, string accountName, string inviteUrl)
        => $"{ownerDisplayName} invited you to collaborate on the shared account '{accountName}' in Ledger Nest.{Environment.NewLine}{Environment.NewLine}Open this secure invite link:{Environment.NewLine}{inviteUrl}{Environment.NewLine}{Environment.NewLine}Sign in or create your account using the same email address to accept access.{Environment.NewLine}This invite expires automatically and can be revoked by the owner at any time.";
}
