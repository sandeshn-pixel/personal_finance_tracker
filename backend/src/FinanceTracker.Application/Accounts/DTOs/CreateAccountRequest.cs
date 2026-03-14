using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Accounts.DTOs;

public sealed class CreateAccountRequest
{
    public string Name { get; init; } = string.Empty;
    public AccountType Type { get; init; }
    public string CurrencyCode { get; init; } = "INR";
    public decimal OpeningBalance { get; init; }
    public string? InstitutionName { get; init; }
    public string? Last4Digits { get; init; }
}
