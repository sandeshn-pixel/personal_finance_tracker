using FinanceTracker.Application.Accounts.DTOs;
using FinanceTracker.Application.Accounts.Interfaces;
using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.Common;
using FinanceTracker.Application.Notifications.DTOs;
using FinanceTracker.Application.Notifications.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Auth;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Financial;

public sealed class AccountMembershipService(
    ApplicationDbContext dbContext,
    AccountAccessService accountAccessService,
    INotificationService notificationService,
    IAccountInviteEmailSender accountInviteEmailSender,
    ITokenGenerator tokenGenerator,
    IConfiguration configuration,
    IOptions<EmailOptions> emailOptions,
    ILogger<AccountMembershipService> logger) : IAccountMembershipService
{
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);
    private readonly string[] _frontendAllowedOrigins = configuration.GetSection("Frontend:AllowedOrigins").Get<string[]>() ?? [];
    private readonly bool _isDevelopment = string.Equals(configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase);
    private readonly EmailOptions _emailOptions = emailOptions.Value;

    public async Task<IReadOnlyCollection<AccountMemberDto>> ListAsync(Guid userId, Guid accountId, CancellationToken cancellationToken)
    {
        var account = await LoadAccountAsync(userId, accountId, requireOwner: false, cancellationToken);
        var owner = account.User;

        var members = new List<AccountMemberDto>
        {
            new(
                owner.Id,
                owner.Email,
                BuildDisplayName(owner),
                AccountMemberRole.Owner,
                true,
                null,
                BuildDisplayName(owner),
                account.CreatedUtc,
                account.UpdatedUtc)
        };

        members.AddRange(account.Memberships
            .OrderBy(m => m.Role)
            .ThenBy(m => m.User.FirstName)
            .ThenBy(m => m.User.LastName)
            .Select(m => new AccountMemberDto(
                m.UserId,
                m.User.Email,
                BuildDisplayName(m.User),
                m.Role,
                false,
                BuildDisplayName(m.InvitedByUser),
                BuildDisplayName(m.LastModifiedByUser),
                m.CreatedUtc,
                m.UpdatedUtc)));

        return members;
    }

    public async Task<IReadOnlyCollection<AccountPendingInviteDto>> ListPendingInvitesAsync(Guid userId, Guid accountId, CancellationToken cancellationToken)
    {
        _ = await LoadAccountAsync(userId, accountId, requireOwner: true, cancellationToken);

        return await dbContext.AccountInvites
            .Include(x => x.InvitedByUser)
            .Where(x => x.AccountId == accountId && x.Status == AccountInviteStatus.Pending)
            .OrderBy(x => x.ExpiresUtc <= DateTime.UtcNow)
            .ThenByDescending(x => x.CreatedUtc)
            .Select(x => new AccountPendingInviteDto(
                x.Id,
                x.Email,
                x.Role,
                BuildDisplayName(x.InvitedByUser),
                x.CreatedUtc,
                x.ExpiresUtc,
                x.ExpiresUtc <= DateTime.UtcNow))
            .ToListAsync(cancellationToken);
    }

    public async Task<InviteAccountMemberResponse> InviteAsync(Guid userId, Guid accountId, InviteAccountMemberRequest request, CancellationToken cancellationToken)
    {
        var account = await LoadAccountAsync(userId, accountId, requireOwner: true, cancellationToken);
        var email = NormalizeEmail(request.Email);

        ValidateInviteTarget(account, email);
        await EnsureInviteTargetAvailableAsync(accountId, email, cancellationToken);

        var rawToken = tokenGenerator.CreateRefreshToken();
        var invite = new AccountInvite
        {
            AccountId = account.Id,
            Email = email,
            Role = request.Role,
            TokenHash = tokenGenerator.HashRefreshToken(rawToken),
            ExpiresUtc = DateTime.UtcNow.Add(InviteLifetime),
            InvitedByUserId = userId,
        };

        dbContext.AccountInvites.Add(invite);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildInviteResponseAsync(account, invite, rawToken, cancellationToken, isResend: false);
    }

    public async Task<InviteAccountMemberResponse> ResendInviteAsync(Guid userId, Guid accountId, Guid inviteId, CancellationToken cancellationToken)
    {
        var account = await LoadAccountAsync(userId, accountId, requireOwner: true, cancellationToken);
        var invite = await dbContext.AccountInvites
            .SingleOrDefaultAsync(x => x.Id == inviteId && x.AccountId == accountId && x.Status == AccountInviteStatus.Pending, cancellationToken)
            ?? throw new NotFoundException("Pending invite was not found.");

        var rawToken = tokenGenerator.CreateRefreshToken();
        invite.TokenHash = tokenGenerator.HashRefreshToken(rawToken);
        invite.ExpiresUtc = DateTime.UtcNow.Add(InviteLifetime);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildInviteResponseAsync(account, invite, rawToken, cancellationToken, isResend: true);
    }

    public async Task<AccountInvitePreviewDto> PreviewInviteAsync(Guid? userId, string token, CancellationToken cancellationToken)
    {
        var invite = await LoadInviteByTokenAsync(token, cancellationToken);

        var status = invite.Status switch
        {
            AccountInviteStatus.Accepted => "Accepted",
            AccountInviteStatus.Revoked => "Revoked",
            _ when invite.ExpiresUtc <= DateTime.UtcNow => "Expired",
            _ => "Pending"
        };

        string statusMessage;
        var canAccept = false;
        var requiresDifferentAccount = false;

        if (status == "Accepted")
        {
            statusMessage = "This invite was already accepted.";
        }
        else if (status == "Revoked")
        {
            statusMessage = "This invite has been revoked by the account owner.";
        }
        else if (status == "Expired")
        {
            statusMessage = "This invite has expired. Ask the account owner to resend it.";
        }
        else if (!userId.HasValue)
        {
            statusMessage = "Sign in or create an account with the invited email to accept this shared-account invite.";
        }
        else
        {
            var user = await dbContext.Users.SingleAsync(x => x.Id == userId.Value, cancellationToken);
            var normalizedUserEmail = NormalizeEmail(user.Email);
            if (!string.Equals(normalizedUserEmail, invite.Email, StringComparison.Ordinal))
            {
                requiresDifferentAccount = true;
                statusMessage = $"This invite was sent to {invite.Email}. Sign in with that email to accept it.";
            }
            else if (invite.Account.UserId == user.Id || await dbContext.AccountMemberships.AnyAsync(x => x.AccountId == invite.AccountId && x.UserId == user.Id, cancellationToken))
            {
                statusMessage = "This account is already available in your workspace.";
            }
            else
            {
                canAccept = true;
                statusMessage = "You can safely accept this invite now. Access will be limited to the role shown here.";
            }
        }

        return new AccountInvitePreviewDto(
            invite.Id,
            invite.AccountId,
            invite.Account.Name,
            BuildDisplayName(invite.Account.User),
            invite.Email,
            invite.Role,
            status,
            invite.ExpiresUtc,
            canAccept,
            requiresDifferentAccount,
            statusMessage);
    }

    public async Task<AccountMemberDto> AcceptInviteAsync(Guid userId, AcceptAccountInviteRequest request, CancellationToken cancellationToken)
    {
        var invite = await LoadAcceptableInviteByTokenAsync(request.Token, cancellationToken);
        var user = await dbContext.Users.SingleAsync(x => x.Id == userId, cancellationToken);

        if (!string.Equals(NormalizeEmail(user.Email), invite.Email, StringComparison.Ordinal))
        {
            throw new ValidationException($"This invite was sent to {invite.Email}. Sign in with that email to accept it.");
        }

        if (invite.Account.UserId == userId)
        {
            throw new ConflictException("The account owner already has access.");
        }

        var existingMembership = await dbContext.AccountMemberships
            .Include(x => x.User)
            .Include(x => x.InvitedByUser)
            .Include(x => x.LastModifiedByUser)
            .SingleOrDefaultAsync(x => x.AccountId == invite.AccountId && x.UserId == userId, cancellationToken);

        if (existingMembership is null)
        {
            existingMembership = new AccountMembership
            {
                AccountId = invite.AccountId,
                UserId = userId,
                Role = invite.Role,
                InvitedByUserId = invite.InvitedByUserId,
                LastModifiedByUserId = invite.InvitedByUserId,
            };

            dbContext.AccountMemberships.Add(existingMembership);
        }

        invite.Status = AccountInviteStatus.Accepted;
        invite.AcceptedByUserId = userId;
        invite.AcceptedUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.Entry(existingMembership).Reference(x => x.User).LoadAsync(cancellationToken);
        await dbContext.Entry(existingMembership).Reference(x => x.InvitedByUser).LoadAsync(cancellationToken);
        await dbContext.Entry(existingMembership).Reference(x => x.LastModifiedByUser).LoadAsync(cancellationToken);

        await notificationService.PublishAsync(new PublishNotificationRequest(
            invite.Account.UserId,
            NotificationType.SharedAccountInvite,
            NotificationLevel.Info,
            $"{BuildDisplayName(user)} accepted your invite",
            $"{BuildDisplayName(user)} joined the shared account '{invite.Account.Name}' as {existingMembership.Role.ToString().ToLowerInvariant()}.",
            $"/accounts/{invite.AccountId}",
            $"shared-account-invite-accepted:{invite.Id}"), cancellationToken);

        return new AccountMemberDto(
            existingMembership.UserId,
            existingMembership.User.Email,
            BuildDisplayName(existingMembership.User),
            existingMembership.Role,
            false,
            BuildDisplayName(existingMembership.InvitedByUser),
            BuildDisplayName(existingMembership.LastModifiedByUser),
            existingMembership.CreatedUtc,
            existingMembership.UpdatedUtc);
    }

    public async Task<AccountMemberDto> UpdateAsync(Guid userId, Guid accountId, Guid memberUserId, UpdateAccountMemberRequest request, CancellationToken cancellationToken)
    {
        var account = await LoadAccountAsync(userId, accountId, requireOwner: true, cancellationToken);
        if (memberUserId == account.UserId)
        {
            throw new ValidationException("The account owner role cannot be changed.");
        }

        var membership = await dbContext.AccountMemberships
            .Include(x => x.User)
            .Include(x => x.InvitedByUser)
            .Include(x => x.LastModifiedByUser)
            .SingleOrDefaultAsync(x => x.AccountId == accountId && x.UserId == memberUserId, cancellationToken)
            ?? throw new NotFoundException("Account member was not found.");

        membership.Role = request.Role;
        membership.LastModifiedByUserId = userId;
        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.Entry(membership).Reference(x => x.LastModifiedByUser).LoadAsync(cancellationToken);

        return new AccountMemberDto(
            membership.UserId,
            membership.User.Email,
            BuildDisplayName(membership.User),
            membership.Role,
            false,
            BuildDisplayName(membership.InvitedByUser),
            BuildDisplayName(membership.LastModifiedByUser),
            membership.CreatedUtc,
            membership.UpdatedUtc);
    }

    public async Task RemoveAsync(Guid userId, Guid accountId, Guid memberUserId, CancellationToken cancellationToken)
    {
        var account = await LoadAccountAsync(userId, accountId, requireOwner: true, cancellationToken);
        if (memberUserId == account.UserId)
        {
            throw new ValidationException("The account owner cannot be removed.");
        }

        var membership = await dbContext.AccountMemberships
            .SingleOrDefaultAsync(x => x.AccountId == accountId && x.UserId == memberUserId, cancellationToken)
            ?? throw new NotFoundException("Account member was not found.");

        dbContext.AccountMemberships.Remove(membership);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeInviteAsync(Guid userId, Guid accountId, Guid inviteId, CancellationToken cancellationToken)
    {
        _ = await LoadAccountAsync(userId, accountId, requireOwner: true, cancellationToken);

        var invite = await dbContext.AccountInvites
            .SingleOrDefaultAsync(x => x.Id == inviteId && x.AccountId == accountId && x.Status == AccountInviteStatus.Pending, cancellationToken)
            ?? throw new NotFoundException("Pending invite was not found.");

        invite.Status = AccountInviteStatus.Revoked;
        invite.RevokedByUserId = userId;
        invite.RevokedUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<InviteAccountMemberResponse> BuildInviteResponseAsync(Account account, AccountInvite invite, string rawToken, CancellationToken cancellationToken, bool isResend)
    {
        var inviteUrl = BuildInviteUrl(rawToken);
        try
        {
            await accountInviteEmailSender.SendInviteAsync(invite.Email, BuildDisplayName(account.User), account.Name, inviteUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to send account invite email for account {AccountId} to {Email}", account.Id, invite.Email);
        }

        var invitee = await dbContext.Users.SingleOrDefaultAsync(x => x.Email.ToLower() == invite.Email, cancellationToken);
        if (invitee is not null)
        {
            await notificationService.PublishAsync(new PublishNotificationRequest(
                invitee.Id,
                NotificationType.SharedAccountInvite,
                NotificationLevel.Info,
                isResend ? $"Shared account invite refreshed for {account.Name}" : $"Shared account invite for {account.Name}",
                isResend
                    ? $"{BuildDisplayName(account.User)} resent your invite as {invite.Role.ToString().ToLowerInvariant()} for the shared account '{account.Name}'."
                    : $"{BuildDisplayName(account.User)} invited you as {invite.Role.ToString().ToLowerInvariant()} for the shared account '{account.Name}'.",
                $"/account-invites/accept?token={Uri.EscapeDataString(rawToken)}",
                isResend ? $"shared-account-invite-resend:{invite.Id}:{invite.ExpiresUtc:O}" : $"shared-account-invite:{invite.Id}"), cancellationToken);
        }

        var inviteDto = new AccountPendingInviteDto(
            invite.Id,
            invite.Email,
            invite.Role,
            BuildDisplayName(account.User),
            invite.CreatedUtc,
            invite.ExpiresUtc,
            invite.ExpiresUtc <= DateTime.UtcNow);

        return new InviteAccountMemberResponse(inviteDto, _isDevelopment && !_emailOptions.Enabled ? inviteUrl : null);
    }

    private async Task EnsureInviteTargetAvailableAsync(Guid accountId, string email, CancellationToken cancellationToken)
    {
        var invitee = await dbContext.Users.SingleOrDefaultAsync(x => x.Email.ToLower() == email, cancellationToken);
        if (invitee is not null)
        {
            var existingMembership = await dbContext.AccountMemberships
                .AnyAsync(x => x.AccountId == accountId && x.UserId == invitee.Id, cancellationToken);

            if (existingMembership)
            {
                throw new ConflictException("That user already has access to this account.");
            }
        }

        var existingInvite = await dbContext.AccountInvites
            .AnyAsync(x => x.AccountId == accountId
                && x.Email == email
                && x.Status == AccountInviteStatus.Pending
                && x.ExpiresUtc > DateTime.UtcNow,
                cancellationToken);

        if (existingInvite)
        {
            throw new ConflictException("A pending invite already exists for that email.");
        }
    }

    private static void ValidateInviteTarget(Account account, string email)
    {
        if (NormalizeEmail(account.User.Email) == email)
        {
            throw new ConflictException("The account owner already has access.");
        }
    }

    private async Task<AccountInvite> LoadInviteByTokenAsync(string rawToken, CancellationToken cancellationToken)
    {
        var tokenHash = tokenGenerator.HashRefreshToken(rawToken.Trim());
        return await dbContext.AccountInvites
            .Include(x => x.Account)
                .ThenInclude(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken)
            ?? throw new NotFoundException("Account invite is unavailable.");
    }

    private async Task<AccountInvite> LoadAcceptableInviteByTokenAsync(string rawToken, CancellationToken cancellationToken)
    {
        var invite = await LoadInviteByTokenAsync(rawToken, cancellationToken);

        if (invite.Status != AccountInviteStatus.Pending || invite.RevokedUtc.HasValue || invite.AcceptedUtc.HasValue)
        {
            throw new ValidationException("This invite is no longer available.");
        }

        if (invite.ExpiresUtc <= DateTime.UtcNow)
        {
            throw new ValidationException("This invite has expired.");
        }

        return invite;
    }

    private async Task<Account> LoadAccountAsync(Guid userId, Guid accountId, bool requireOwner, CancellationToken cancellationToken)
    {
        IQueryable<Account> query = dbContext.Accounts
            .Include(x => x.User)
            .Include(x => x.Memberships)
                .ThenInclude(x => x.User)
            .Include(x => x.Memberships)
                .ThenInclude(x => x.InvitedByUser)
            .Include(x => x.Memberships)
                .ThenInclude(x => x.LastModifiedByUser)
            .Include(x => x.Invites)
                .ThenInclude(x => x.InvitedByUser);

        if (requireOwner)
        {
            return await query.SingleOrDefaultAsync(x => x.Id == accountId && x.UserId == userId, cancellationToken)
                ?? throw new NotFoundException("Account was not found.");
        }

        var accessible = await accountAccessService.FindAccessibleAccountAsync(userId, accountId, AccountMemberRole.Viewer, includeArchived: true, cancellationToken);
        if (accessible is null)
        {
            throw new NotFoundException("Account was not found.");
        }

        return await query.SingleAsync(x => x.Id == accountId, cancellationToken);
    }

    private string BuildInviteUrl(string token)
    {
        var frontendOrigin = _isDevelopment
            ? "http://localhost:5173"
            : _frontendAllowedOrigins.FirstOrDefault(origin => Uri.TryCreate(origin, UriKind.Absolute, out _)) ?? "http://localhost:5173";

        return $"{frontendOrigin.TrimEnd('/')}/account-invites/accept?token={Uri.EscapeDataString(token)}";
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string BuildDisplayName(User user)
    {
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }
}
