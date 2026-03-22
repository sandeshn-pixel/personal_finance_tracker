using FinanceTracker.Application.Forecasting.DTOs;

namespace FinanceTracker.Application.Forecasting.Interfaces;

public interface IForecastService
{
    Task<ForecastMonthSummaryDto> GetMonthSummaryAsync(Guid userId, ForecastQuery query, CancellationToken cancellationToken);
    Task<ForecastDailyResponseDto> GetDailyProjectionAsync(Guid userId, ForecastQuery query, CancellationToken cancellationToken);
}
