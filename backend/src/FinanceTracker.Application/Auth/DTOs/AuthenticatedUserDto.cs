namespace FinanceTracker.Application.Auth.DTOs;

public sealed record AuthenticatedUserDto(Guid Id, string Email, string FirstName, string LastName);
