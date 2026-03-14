namespace FinanceTracker.Application.Auth.DTOs;

public sealed record AuthResponse(string AccessToken, int ExpiresInSeconds, AuthenticatedUserDto User);
