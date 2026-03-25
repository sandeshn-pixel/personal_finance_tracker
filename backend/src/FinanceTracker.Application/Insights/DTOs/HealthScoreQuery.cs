namespace FinanceTracker.Application.Insights.DTOs;

public sealed record HealthScoreQuery(Guid[]? AccountIds, Guid? AccountId);
