using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Accounts.DTOs;

public sealed record AccountDto(
    Guid Id,
    string Name,
    AccountType Type,
    string CurrencyCode,
    decimal OpeningBalance,
    decimal CurrentBalance,
    string? InstitutionName,
    string? Last4Digits,
    bool IsArchived);
