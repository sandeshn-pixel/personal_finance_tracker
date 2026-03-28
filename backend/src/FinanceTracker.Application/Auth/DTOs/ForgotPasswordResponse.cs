namespace FinanceTracker.Application.Auth.DTOs;

public sealed record ForgotPasswordResponse(string Message, string? ResetUrl, string? DebugStatus);
