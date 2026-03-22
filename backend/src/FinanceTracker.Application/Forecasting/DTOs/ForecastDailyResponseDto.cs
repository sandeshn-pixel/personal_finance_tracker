namespace FinanceTracker.Application.Forecasting.DTOs;

public sealed record ForecastDailyResponseDto(
    ForecastMonthSummaryDto Summary,
    IReadOnlyCollection<ForecastDayPointDto> Points);
