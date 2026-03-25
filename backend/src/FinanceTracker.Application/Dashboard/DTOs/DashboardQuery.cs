namespace FinanceTracker.Application.Dashboard.DTOs;

public sealed record DashboardQuery(Guid[]? AccountIds, Guid? AccountId);
