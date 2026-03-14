namespace FinanceTracker.Application.Reports.Interfaces;

using FinanceTracker.Application.Reports.DTOs;

public interface IReportService
{
    Task<ReportsOverviewDto> GetOverviewAsync(Guid userId, ReportQuery query, CancellationToken cancellationToken);
}
