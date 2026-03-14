using FinanceTracker.Application.Auth.DTOs;

namespace FinanceTracker.Application.Auth.Interfaces;

public interface IAuthService
{
    Task<AuthEnvelope> RegisterAsync(RegisterRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<AuthEnvelope> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<AuthEnvelope> RefreshAsync(string refreshToken, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);
    Task<AuthenticatedUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
}
