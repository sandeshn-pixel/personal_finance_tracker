using FinanceTracker.Application.Dashboard.DTOs;

namespace FinanceTracker.Application.Dashboard.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(Guid userId, CancellationToken cancellationToken);
}
