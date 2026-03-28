using System.Net;
using System.Net.Mail;
using FinanceTracker.Application.Auth.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Auth;

public sealed class PasswordResetEmailSender(
    IOptions<EmailOptions> emailOptions,
    ILogger<PasswordResetEmailSender> logger) : IPasswordResetEmailSender
{
    private readonly EmailOptions _emailOptions = emailOptions.Value;

    public async Task SendResetLinkAsync(string email, string resetUrl, CancellationToken cancellationToken)
    {
        if (!_emailOptions.Enabled)
        {
            logger.LogInformation("Password reset email delivery is disabled. Reset link for {Email}: {ResetUrl}", email, resetUrl);
            return;
        }

        logger.LogInformation(
            "Attempting to send password reset email to {Email} using SMTP host {SmtpHost}:{Port} with SSL {UseSsl}.",
            email,
            _emailOptions.SmtpHost,
            _emailOptions.Port,
            _emailOptions.UseSsl);

        using var message = new MailMessage
        {
            From = new MailAddress(_emailOptions.FromAddress!, _emailOptions.FromName),
            Subject = "Reset your Ledger Nest password",
            Body = BuildTextBody(resetUrl),
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

        try
        {
            await client.SendMailAsync(message, cancellationToken);
            logger.LogInformation("Password reset email accepted by SMTP provider for {Email}.", email);
        }
        catch (Exception ex)
        {
            logger.LogError(
                "Password reset email send failed for {Email}. SMTP host {SmtpHost}:{Port}. Error: {Error}",
                email,
                _emailOptions.SmtpHost,
                _emailOptions.Port,
                ex.Message);
            throw;
        }
    }

    private static string BuildTextBody(string resetUrl)
        => $"A password reset was requested for your Ledger Nest account.{Environment.NewLine}{Environment.NewLine}Open this secure reset link:{Environment.NewLine}{resetUrl}{Environment.NewLine}{Environment.NewLine}This link expires automatically and can only be used once.{Environment.NewLine}If you did not request this change, you can ignore this email.";
}
