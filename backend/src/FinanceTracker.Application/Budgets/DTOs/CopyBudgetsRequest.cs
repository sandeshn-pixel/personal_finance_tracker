namespace FinanceTracker.Application.Budgets.DTOs;

public sealed record CopyBudgetsRequest(int Year, int Month, bool OverwriteExisting = false);
