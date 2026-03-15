using FinanceTracker.Application.Auth.DTOs;
using FinanceTracker.Infrastructure.Auth;
using FinanceTracker.Infrastructure.Financial;
using FinanceTracker.Backend.Tests.TestSupport;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Backend.Tests;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task RegisterLoginAndRefresh_SeedDefaultsAndRotateRefreshToken()
    {
        await using var database = new SqliteTestDatabase();
        await using var dbContext = database.CreateContext();
        var tokenGenerator = new TokenGenerator(Options.Create(new JwtOptions
        {
            Issuer = "tests",
            Audience = "tests",
            SigningKey = "12345678901234567890123456789012",
            AccessTokenLifetimeMinutes = 15,
            RefreshTokenLifetimeDays = 7,
            RefreshCookieName = "ft_refresh",
            RequireHttpsMetadata = false
        }));
        var service = new AuthService(dbContext, new PasswordHasher<FinanceTracker.Domain.Entities.User>(), tokenGenerator, new CategorySeeder(dbContext));

        var register = await service.RegisterAsync(new RegisterRequest
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            Password = "Abcd1234567@#"
        }, "127.0.0.1", "tests", CancellationToken.None);

        var login = await service.LoginAsync(new LoginRequest
        {
            Email = "ada@example.com",
            Password = "Abcd1234567@#"
        }, "127.0.0.1", "tests", CancellationToken.None);

        var refresh = await service.RefreshAsync(register.RefreshToken, "127.0.0.1", "tests", CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(register.Response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(login.Response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(refresh.Response.AccessToken));
        Assert.NotEqual(register.RefreshToken, refresh.RefreshToken);
        Assert.True(await dbContext.Categories.CountAsync(x => x.UserId == register.Response.User.Id) >= 18);
        Assert.True(await dbContext.RefreshTokens.CountAsync(x => x.UserId == register.Response.User.Id) >= 3);
        Assert.True(await dbContext.RefreshTokens.AnyAsync(x => x.UserId == register.Response.User.Id && x.RevokedUtc != null));
    }
}
