namespace FinanceTracker.Application.Budgets.DTOs;

public sealed record CreateBudgetRequest(
    Guid CategoryId,
    int Year,
    int Month,
    decimal Amount,
    int AlertThresholdPercent);
