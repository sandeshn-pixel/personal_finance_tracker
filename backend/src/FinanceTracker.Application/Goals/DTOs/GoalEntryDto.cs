using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Goals.DTOs;

public sealed record GoalEntryDto(
    Guid Id,
    GoalEntryType Type,
    decimal Amount,
    decimal GoalAmountAfterEntry,
    DateTime OccurredAtUtc,
    string? Note,
    Guid? AccountId,
    string? AccountName,
    DateTime CreatedUtc);