namespace FinanceTracker.Application.Budgets.DTOs;

public sealed record UpdateBudgetRequest(
    decimal Amount,
    int AlertThresholdPercent);
