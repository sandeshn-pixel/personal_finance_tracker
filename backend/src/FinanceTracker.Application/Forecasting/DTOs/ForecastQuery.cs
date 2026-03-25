namespace FinanceTracker.Application.Forecasting.DTOs;

public sealed record ForecastQuery(Guid[]? AccountIds, Guid? AccountId);
