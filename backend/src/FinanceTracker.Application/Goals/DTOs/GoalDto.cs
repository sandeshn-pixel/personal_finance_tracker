using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Goals.DTOs;

public sealed record GoalDto(
    Guid Id,
    string Name,
    decimal TargetAmount,
    decimal CurrentAmount,
    decimal RemainingAmount,
    decimal ProgressPercent,
    DateTime? TargetDateUtc,
    Guid? LinkedAccountId,
    string? LinkedAccountName,
    string? Icon,
    string? Color,
    GoalStatus Status,
    bool CanManage,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);
