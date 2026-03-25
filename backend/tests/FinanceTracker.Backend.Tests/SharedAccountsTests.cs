using FinanceTracker.Application.Accounts.DTOs;
using FinanceTracker.Application.Accounts.Interfaces;
using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.Common;
using FinanceTracker.Backend.Tests.TestSupport;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Auth;
using FinanceTracker.Infrastructure.Financial;
using FinanceTracker.Infrastructure.Notifications;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Backend.Tests;

public sealed class SharedAccountsTests
{
    [Fact]
    public async Task AccountMembershipService_InviteAndAcceptAsync_CreatesPendingInviteThenMembership()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var owner = TestData.AddUser(dbContext, "owner@example.com");
        owner.FirstName = "Owner";
        owner.LastName = "User";
        var invitee = TestData.AddUser(dbContext, "member@example.com");
        invitee.FirstName = "Member";
        invitee.LastName = "Viewer";
        var account = TestData.AddAccount(dbContext, owner.Id, "Household", 2000m);
        await dbContext.SaveChangesAsync();

        var tokenGenerator = new FakeTokenGenerator();
        var service = CreateMembershipService(dbContext, tokenGenerator);
        var response = await service.InviteAsync(owner.Id, account.Id, new InviteAccountMemberRequest(invitee.Email, AccountMemberRole.Viewer), CancellationToken.None);

        Assert.Equal("member@example.com", response.Invite.Email);
        Assert.Equal(AccountMemberRole.Viewer, response.Invite.Role);
        Assert.Empty(await dbContext.AccountMemberships.ToListAsync());
        Assert.Single(await dbContext.AccountInvites.ToListAsync());
        Assert.Equal(NotificationType.SharedAccountInvite, Assert.Single(dbContext.UserNotifications).Type);

        var accepted = await service.AcceptInviteAsync(invitee.Id, new AcceptAccountInviteRequest(tokenGenerator.LastIssuedToken!), CancellationToken.None);
        var members = await service.ListAsync(invitee.Id, account.Id, CancellationToken.None);

        Assert.Equal(invitee.Id, accepted.UserId);
        Assert.Equal(2, members.Count);
        Assert.Contains(members, x => !x.IsOwner && x.UserId == invitee.Id && x.Role == AccountMemberRole.Viewer);
        Assert.Equal(AccountInviteStatus.Accepted, Assert.Single(dbContext.AccountInvites).Status);
    }

    [Fact]
    public async Task AccountMembershipService_ResendInviteAsync_ExtendsExpiryAndIssuesNewToken()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var owner = TestData.AddUser(dbContext, "owner@example.com");
        var invitee = TestData.AddUser(dbContext, "member@example.com");
        var account = TestData.AddAccount(dbContext, owner.Id, "Household", 2000m);
        await dbContext.SaveChangesAsync();

        var tokenGenerator = new FakeTokenGenerator();
        var service = CreateMembershipService(dbContext, tokenGenerator);
        var firstResponse = await service.InviteAsync(owner.Id, account.Id, new InviteAccountMemberRequest(invitee.Email, AccountMemberRole.Editor), CancellationToken.None);
        var firstToken = tokenGenerator.LastIssuedToken;
        var invite = await dbContext.AccountInvites.SingleAsync();
        invite.ExpiresUtc = DateTime.UtcNow.AddHours(-2);
        await dbContext.SaveChangesAsync();

        var resent = await service.ResendInviteAsync(owner.Id, account.Id, invite.Id, CancellationToken.None);
        var updated = await dbContext.AccountInvites.SingleAsync();

        Assert.Equal(firstResponse.Invite.Id, resent.Invite.Id);
        Assert.NotEqual(firstToken, tokenGenerator.LastIssuedToken);
        Assert.True(updated.ExpiresUtc > DateTime.UtcNow.AddDays(6));
        Assert.False(resent.Invite.IsExpired);
    }

    [Fact]
    public async Task AccountMembershipService_PreviewInviteAsync_ShowsExpiredState()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var owner = TestData.AddUser(dbContext, "owner@example.com");
        var invitee = TestData.AddUser(dbContext, "member@example.com");
        var account = TestData.AddAccount(dbContext, owner.Id, "Household", 2000m);
        await dbContext.SaveChangesAsync();

        var tokenGenerator = new FakeTokenGenerator();
        var service = CreateMembershipService(dbContext, tokenGenerator);
        await service.InviteAsync(owner.Id, account.Id, new InviteAccountMemberRequest(invitee.Email, AccountMemberRole.Viewer), CancellationToken.None);

        var invite = await dbContext.AccountInvites.SingleAsync();
        invite.ExpiresUtc = DateTime.UtcNow.AddMinutes(-1);
        await dbContext.SaveChangesAsync();

        var preview = await service.PreviewInviteAsync(null, tokenGenerator.LastIssuedToken!, CancellationToken.None);

        Assert.Equal("Expired", preview.Status);
        Assert.False(preview.CanAccept);
        Assert.Contains("expired", preview.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AccountMembershipService_AcceptInviteAsync_RejectsWrongEmail()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var owner = TestData.AddUser(dbContext, "owner@example.com");
        var invitee = TestData.AddUser(dbContext, "member@example.com");
        var otherUser = TestData.AddUser(dbContext, "other@example.com");
        var account = TestData.AddAccount(dbContext, owner.Id, "Household", 2000m);
        await dbContext.SaveChangesAsync();

        var tokenGenerator = new FakeTokenGenerator();
        var service = CreateMembershipService(dbContext, tokenGenerator);
        await service.InviteAsync(owner.Id, account.Id, new InviteAccountMemberRequest(invitee.Email, AccountMemberRole.Editor), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.AcceptInviteAsync(otherUser.Id, new AcceptAccountInviteRequest(tokenGenerator.LastIssuedToken!), CancellationToken.None));
        Assert.Contains("sent to member@example.com", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AccountService_ListAsync_IncludesSharedAccountForViewer()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var owner = TestData.AddUser(dbContext, "owner@example.com");
        owner.FirstName = "Owner";
        owner.LastName = "User";
        var viewer = TestData.AddUser(dbContext, "viewer@example.com");
        viewer.FirstName = "Viewer";
        viewer.LastName = "User";
        var account = TestData.AddAccount(dbContext, owner.Id, "Shared household", 1200m);
        dbContext.AccountMemberships.Add(new AccountMembership
        {
            AccountId = account.Id,
            UserId = viewer.Id,
            Role = AccountMemberRole.Viewer,
            InvitedByUserId = owner.Id,
            LastModifiedByUserId = owner.Id
        });
        await dbContext.SaveChangesAsync();

        var service = new AccountService(dbContext, new AccountAccessService(dbContext));
        var accounts = await service.ListAsync(viewer.Id, includeArchived: false, CancellationToken.None);
        var shared = Assert.Single(accounts);

        Assert.Equal(account.Id, shared.Id);
        Assert.True(shared.IsShared);
        Assert.Equal(AccountMemberRole.Viewer, shared.CurrentUserRole);
        Assert.Equal("Owner User", shared.OwnerDisplayName);
    }

    [Fact]
    public async Task TransactionService_CreateAsync_AllowsSharedEditorAndRejectsViewer()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var owner = TestData.AddUser(dbContext, "owner@example.com");
        var editor = TestData.AddUser(dbContext, "editor@example.com");
        var viewer = TestData.AddUser(dbContext, "viewer@example.com");
        var account = TestData.AddAccount(dbContext, owner.Id, "Family Checking", 3000m);
        var editorCategory = TestData.AddCategory(dbContext, editor.Id, "Shared Groceries", CategoryType.Expense);

        dbContext.AccountMemberships.AddRange(
            new AccountMembership
            {
                AccountId = account.Id,
                UserId = editor.Id,
                Role = AccountMemberRole.Editor,
                InvitedByUserId = owner.Id,
                LastModifiedByUserId = owner.Id
            },
            new AccountMembership
            {
                AccountId = account.Id,
                UserId = viewer.Id,
                Role = AccountMemberRole.Viewer,
                InvitedByUserId = owner.Id,
                LastModifiedByUserId = owner.Id
            });
        await dbContext.SaveChangesAsync();

        var service = new TransactionService(dbContext, new CategorySeeder(dbContext), new AccountAccessService(dbContext));
        var created = await service.CreateAsync(editor.Id, new FinanceTracker.Application.Transactions.DTOs.UpsertTransactionRequest
        {
            AccountId = account.Id,
            Type = TransactionType.Expense,
            Amount = 250m,
            DateUtc = new DateTime(2026, 3, 23, 0, 0, 0, DateTimeKind.Utc),
            CategoryId = editorCategory.Id,
            Merchant = "Shared Market",
            Tags = []
        }, CancellationToken.None);

        Assert.Equal(editor.Id, created.CreatedByUserId);
        Assert.Equal(2750m, (await dbContext.Accounts.SingleAsync(x => x.Id == account.Id)).CurrentBalance);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(viewer.Id, new FinanceTracker.Application.Transactions.DTOs.UpsertTransactionRequest
        {
            AccountId = account.Id,
            Type = TransactionType.Expense,
            Amount = 50m,
            DateUtc = new DateTime(2026, 3, 23, 0, 0, 0, DateTimeKind.Utc),
            CategoryId = editorCategory.Id,
            Merchant = "Should fail",
            Tags = []
        }, CancellationToken.None));

        Assert.Contains("inaccessible", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AccountMembershipService CreateMembershipService(ApplicationDbContext dbContext, FakeTokenGenerator tokenGenerator)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DOTNET_ENVIRONMENT"] = "Development",
                ["Frontend:AllowedOrigins:0"] = "http://localhost:5173",
            })
            .Build();

        return new AccountMembershipService(
            dbContext,
            new AccountAccessService(dbContext),
            new NotificationService(dbContext),
            new FakeAccountInviteEmailSender(),
            tokenGenerator,
            configuration,
            Options.Create(new EmailOptions { Enabled = false }),
            NullLogger<AccountMembershipService>.Instance);
    }

    private sealed class FakeAccountInviteEmailSender : IAccountInviteEmailSender
    {
        public Task SendInviteAsync(string email, string ownerDisplayName, string accountName, string inviteUrl, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeTokenGenerator : ITokenGenerator
    {
        public string? LastIssuedToken { get; private set; }

        public string CreateAccessToken(User user) => "access-token";

        public int GetAccessTokenLifetimeSeconds() => 900;

        public string CreateRefreshToken()
        {
            LastIssuedToken = $"token-{Guid.NewGuid():N}";
            return LastIssuedToken;
        }

        public string HashRefreshToken(string refreshToken) => $"hash::{refreshToken.Trim()}";

        public DateTime GetRefreshTokenExpiryUtc() => DateTime.UtcNow.AddDays(7);
    }
}

