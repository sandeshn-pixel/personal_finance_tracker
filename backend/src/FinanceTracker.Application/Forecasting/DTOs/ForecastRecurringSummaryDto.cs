namespace FinanceTracker.Application.Forecasting.DTOs;

public sealed record ForecastRecurringSummaryDto(
    decimal TotalExpectedIncome,
    decimal TotalExpectedExpense,
    decimal NetExpectedImpact,
    int ItemCount,
    IReadOnlyCollection<ForecastRecurringItemDto> Items);
