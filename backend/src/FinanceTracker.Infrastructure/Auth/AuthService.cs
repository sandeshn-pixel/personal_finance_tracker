using FinanceTracker.Application.Auth.DTOs;
using FinanceTracker.Application.Auth.Exceptions;
using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.Categories.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Auth;

public sealed class AuthService(
    ApplicationDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    ITokenGenerator tokenGenerator,
    ICategorySeeder categorySeeder) : IAuthService
{
    public async Task<AuthEnvelope> RegisterAsync(RegisterRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailExists = await dbContext.Users.AnyAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            throw new AuthException("An account with this email already exists.");
        }

        var user = new User
        {
            Email = normalizedEmail,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim()
        };

        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        dbContext.Users.Add(user);
        await categorySeeder.EnsureDefaultsAsync(user, cancellationToken);

        var envelope = await IssueSessionAsync(user, ipAddress, userAgent, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return envelope;
    }

    public async Task<AuthEnvelope> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            throw new AuthException("Invalid credentials.");
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result is PasswordVerificationResult.Failed)
        {
            throw new AuthException("Invalid credentials.");
        }

        if (result is PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        }

        user.LastLoginUtc = DateTime.UtcNow;
        var envelope = await IssueSessionAsync(user, ipAddress, userAgent, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return envelope;
    }

    public async Task<AuthEnvelope> RefreshAsync(string refreshToken, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var tokenHash = tokenGenerator.HashRefreshToken(refreshToken);
        var existingToken = await dbContext.RefreshTokens
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (existingToken is null || existingToken.RevokedUtc is not null || existingToken.ExpiresUtc <= DateTime.UtcNow)
        {
            throw new AuthException("Refresh session is invalid or expired.");
        }

        var replacementRefreshToken = tokenGenerator.CreateRefreshToken();
        var replacementHash = tokenGenerator.HashRefreshToken(replacementRefreshToken);
        var replacementExpiryUtc = tokenGenerator.GetRefreshTokenExpiryUtc();

        existingToken.RevokedUtc = DateTime.UtcNow;
        existingToken.ReplacedByTokenHash = replacementHash;
        existingToken.RevocationReason = "Rotated";

        var newToken = new RefreshToken
        {
            UserId = existingToken.UserId,
            TokenHash = replacementHash,
            SessionId = existingToken.SessionId,
            ExpiresUtc = replacementExpiryUtc,
            DeviceName = existingToken.DeviceName,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        dbContext.RefreshTokens.Add(newToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateEnvelope(existingToken.User, replacementRefreshToken, replacementExpiryUtc);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = tokenGenerator.HashRefreshToken(refreshToken);
        var existingToken = await dbContext.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (existingToken is null || existingToken.RevokedUtc is not null)
        {
            return;
        }

        existingToken.RevokedUtc = DateTime.UtcNow;
        existingToken.RevocationReason = "User logout";
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthenticatedUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .Where(x => x.Id == userId)
            .Select(x => new AuthenticatedUserDto(x.Id, x.Email, x.FirstName, x.LastName))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<AuthEnvelope> IssueSessionAsync(User user, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var refreshToken = tokenGenerator.CreateRefreshToken();
        var refreshTokenHash = tokenGenerator.HashRefreshToken(refreshToken);
        var refreshTokenExpiryUtc = tokenGenerator.GetRefreshTokenExpiryUtc();

        var session = new RefreshToken
        {
            User = user,
            TokenHash = refreshTokenHash,
            SessionId = Guid.NewGuid().ToString("N"),
            ExpiresUtc = refreshTokenExpiryUtc,
            DeviceName = "browser",
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        dbContext.RefreshTokens.Add(session);
        await RevokeExpiredAndSupersededSessionsAsync(user.Id, cancellationToken);

        return CreateEnvelope(user, refreshToken, refreshTokenExpiryUtc);
    }

    private async Task RevokeExpiredAndSupersededSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var staleTokens = await dbContext.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedUtc == null && x.ExpiresUtc <= DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var staleToken in staleTokens)
        {
            staleToken.RevokedUtc = DateTime.UtcNow;
            staleToken.RevocationReason = "Expired";
        }
    }

    private AuthEnvelope CreateEnvelope(User user, string refreshToken, DateTime refreshTokenExpiryUtc)
    {
        var accessToken = tokenGenerator.CreateAccessToken(user);
        var response = new AuthResponse(
            accessToken,
            tokenGenerator.GetAccessTokenLifetimeSeconds(),
            new AuthenticatedUserDto(user.Id, user.Email, user.FirstName, user.LastName));

        return new AuthEnvelope(response, refreshToken, refreshTokenExpiryUtc);
    }
}
