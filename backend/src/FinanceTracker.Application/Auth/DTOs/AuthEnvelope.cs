namespace FinanceTracker.Application.Auth.DTOs;

public sealed record AuthEnvelope(AuthResponse Response, string RefreshToken, DateTime RefreshTokenExpiresUtc);
