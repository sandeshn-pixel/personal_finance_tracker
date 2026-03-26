namespace FinanceTracker.Application.Reports.Interfaces;

using FinanceTracker.Application.Reports.DTOs;

public interface IReportService
{
    Task<ReportsOverviewDto> GetOverviewAsync(Guid userId, ReportQuery query, CancellationToken cancellationToken);
    Task<ReportsTrendResponseDto> GetTrendsAsync(Guid userId, ReportTrendsQuery query, CancellationToken cancellationToken);
    Task<NetWorthReportDto> GetNetWorthAsync(Guid userId, ReportNetWorthQuery query, CancellationToken cancellationToken);
}
