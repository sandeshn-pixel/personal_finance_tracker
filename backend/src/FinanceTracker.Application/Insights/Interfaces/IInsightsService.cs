using FinanceTracker.Application.Insights.DTOs;

namespace FinanceTracker.Application.Insights.Interfaces;

public interface IInsightsService
{
    Task<InsightsResponseDto> GetAsync(Guid userId, InsightsQuery query, CancellationToken cancellationToken);
}
