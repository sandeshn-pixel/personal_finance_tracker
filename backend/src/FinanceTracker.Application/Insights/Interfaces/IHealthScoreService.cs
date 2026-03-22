using FinanceTracker.Application.Insights.DTOs;

namespace FinanceTracker.Application.Insights.Interfaces;

public interface IHealthScoreService
{
    Task<HealthScoreResponseDto> GetAsync(Guid userId, CancellationToken cancellationToken);
}
