namespace FinanceTracker.Application.Forecasting.DTOs;

public sealed record ForecastDayPointDto(
    DateTime DateUtc,
    decimal ProjectedBalance,
    decimal BaselineNetChange,
    decimal RecurringNetChange);
