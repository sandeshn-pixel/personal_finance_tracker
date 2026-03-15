using FinanceTracker.Application.Budgets.DTOs;
using FinanceTracker.Application.Exports.DTOs;
using FinanceTracker.Application.Reports.DTOs;
using FinanceTracker.Application.Transactions.DTOs;

namespace FinanceTracker.Application.Exports.Interfaces;

public interface IExportService
{
    Task<ExportFileDto> ExportTransactionsCsvAsync(Guid userId, TransactionListQuery query, CancellationToken cancellationToken);
    Task<ExportFileDto> ExportReportOverviewCsvAsync(Guid userId, ReportQuery query, CancellationToken cancellationToken);
    Task<ExportFileDto> ExportBudgetSummaryCsvAsync(Guid userId, BudgetMonthQuery query, CancellationToken cancellationToken);
}