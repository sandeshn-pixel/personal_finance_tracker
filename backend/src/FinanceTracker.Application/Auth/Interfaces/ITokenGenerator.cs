using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Auth.Interfaces;

public interface ITokenGenerator
{
    string CreateAccessToken(User user);
    int GetAccessTokenLifetimeSeconds();
    string CreateRefreshToken();
    string HashRefreshToken(string refreshToken);
    DateTime GetRefreshTokenExpiryUtc();
}
