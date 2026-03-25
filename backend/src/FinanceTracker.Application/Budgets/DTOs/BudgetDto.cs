namespace FinanceTracker.Application.Budgets.DTOs;

public sealed record BudgetDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    bool CategoryIsArchived,
    int Year,
    int Month,
    decimal Amount,
    int AlertThresholdPercent,
    decimal ActualSpent,
    decimal Remaining,
    decimal PercentageUsed,
    bool IsOverBudget,
    bool IsThresholdReached,
    bool CanManage,
    string OwnerDisplayName);

public sealed record BudgetMonthSummaryDto(
    int Year,
    int Month,
    decimal TotalBudgeted,
    decimal TotalSpent,
    decimal TotalRemaining,
    int OverBudgetCount,
    int ThresholdReachedCount);
