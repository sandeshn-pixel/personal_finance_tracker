using System.Globalization;
using System.Text;
using FinanceTracker.Application.Budgets.DTOs;
using FinanceTracker.Application.Budgets.Interfaces;
using FinanceTracker.Application.Exports.DTOs;
using FinanceTracker.Application.Exports.Interfaces;
using FinanceTracker.Application.Reports.DTOs;
using FinanceTracker.Application.Reports.Interfaces;
using FinanceTracker.Application.Transactions.DTOs;
using FinanceTracker.Infrastructure.Financial;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Reporting;

public sealed class ExportService(
    ApplicationDbContext dbContext,
    IReportService reportService,
    IBudgetService budgetService) : IExportService
{
    private const string CsvContentType = "text/csv";

    public async Task<ExportFileDto> ExportTransactionsCsvAsync(Guid userId, TransactionListQuery query, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Transactions
            .AsNoTracking()
            .ApplyFilters(userId, query)
            .Include(x => x.Account)
            .Include(x => x.TransferAccount)
            .Include(x => x.Category)
            .Include(x => x.Tags)
            .OrderByDescending(x => x.DateUtc)
            .ThenByDescending(x => x.CreatedUtc)
            .ToListAsync(cancellationToken);

        var csv = new StringBuilder();
        WriteRow(csv, "DateUtc", "Type", "Amount", "Account", "TransferAccount", "Category", "Merchant", "Note", "PaymentMethod", "Tags", "Source");
        foreach (var row in rows)
        {
            WriteRow(csv,
                row.DateUtc.ToString("O", CultureInfo.InvariantCulture),
                row.Type.ToString(),
                row.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                row.Account.Name,
                row.TransferAccount?.Name ?? string.Empty,
                row.Category?.Name ?? string.Empty,
                row.Merchant ?? string.Empty,
                row.Note ?? string.Empty,
                row.PaymentMethod ?? string.Empty,
                string.Join(", ", row.Tags.OrderBy(t => t.Value).Select(t => t.Value)),
                row.RecurringTransactionId.HasValue ? "Recurring" : "Manual");
        }

        return BuildFile($"transactions-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv", csv);
    }

    public async Task<ExportFileDto> ExportReportOverviewCsvAsync(Guid userId, ReportQuery query, CancellationToken cancellationToken)
    {
        var overview = await reportService.GetOverviewAsync(userId, query, cancellationToken);
        var csv = new StringBuilder();

        WriteRow(csv, "Section", "Metric", "Value");
        WriteRow(csv, "Summary", "Total income", overview.Summary.TotalIncome.ToString("0.00", CultureInfo.InvariantCulture));
        WriteRow(csv, "Summary", "Total expense", overview.Summary.TotalExpense.ToString("0.00", CultureInfo.InvariantCulture));
        WriteRow(csv, "Summary", "Net cash flow", overview.Summary.NetCashFlow.ToString("0.00", CultureInfo.InvariantCulture));
        WriteRow(csv, "Summary", "Income transaction count", overview.Summary.IncomeTransactionCount.ToString(CultureInfo.InvariantCulture));
        WriteRow(csv, "Summary", "Expense transaction count", overview.Summary.ExpenseTransactionCount.ToString(CultureInfo.InvariantCulture));
        csv.AppendLine();

        WriteRow(csv, "CategorySpend", "Category", "Amount");
        foreach (var item in overview.CategorySpend)
        {
            WriteRow(csv, "CategorySpend", item.CategoryName, item.Amount.ToString("0.00", CultureInfo.InvariantCulture));
        }

        csv.AppendLine();
        WriteRow(csv, "IncomeExpenseTrend", "Label", "Income", "Expense");
        foreach (var point in overview.IncomeExpenseTrend)
        {
            WriteRow(csv, "IncomeExpenseTrend", point.Label, point.Income.ToString("0.00", CultureInfo.InvariantCulture), point.Expense.ToString("0.00", CultureInfo.InvariantCulture));
        }

        csv.AppendLine();
        WriteRow(csv, "AccountBalanceTrend", "Label", "Balance");
        foreach (var point in overview.AccountBalanceTrend)
        {
            WriteRow(csv, "AccountBalanceTrend", point.Label, point.Balance.ToString("0.00", CultureInfo.InvariantCulture));
        }

        return BuildFile($"reports-overview-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv", csv);
    }

    public async Task<ExportFileDto> ExportBudgetSummaryCsvAsync(Guid userId, BudgetMonthQuery query, CancellationToken cancellationToken)
    {
        var summary = await budgetService.GetSummaryAsync(userId, query, cancellationToken);
        var budgets = await budgetService.ListByMonthAsync(userId, query, cancellationToken);

        var csv = new StringBuilder();
        WriteRow(csv, "Section", "Metric", "Value");
        WriteRow(csv, "Summary", "Year", summary.Year.ToString(CultureInfo.InvariantCulture));
        WriteRow(csv, "Summary", "Month", summary.Month.ToString(CultureInfo.InvariantCulture));
        WriteRow(csv, "Summary", "Total budgeted", summary.TotalBudgeted.ToString("0.00", CultureInfo.InvariantCulture));
        WriteRow(csv, "Summary", "Total spent", summary.TotalSpent.ToString("0.00", CultureInfo.InvariantCulture));
        WriteRow(csv, "Summary", "Total remaining", summary.TotalRemaining.ToString("0.00", CultureInfo.InvariantCulture));
        WriteRow(csv, "Summary", "Over budget count", summary.OverBudgetCount.ToString(CultureInfo.InvariantCulture));
        WriteRow(csv, "Summary", "Threshold reached count", summary.ThresholdReachedCount.ToString(CultureInfo.InvariantCulture));
        csv.AppendLine();

        WriteRow(csv, "Budget", "Category", "Budgeted", "ActualSpent", "Remaining", "PercentageUsed", "ThresholdPercent", "IsOverBudget", "ThresholdReached", "CategoryArchived");
        foreach (var budget in budgets.OrderBy(x => x.CategoryName))
        {
            WriteRow(csv,
                "Budget",
                budget.CategoryName,
                budget.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                budget.ActualSpent.ToString("0.00", CultureInfo.InvariantCulture),
                budget.Remaining.ToString("0.00", CultureInfo.InvariantCulture),
                budget.PercentageUsed.ToString("0.00", CultureInfo.InvariantCulture),
                budget.AlertThresholdPercent.ToString(CultureInfo.InvariantCulture),
                budget.IsOverBudget ? "Yes" : "No",
                budget.IsThresholdReached ? "Yes" : "No",
                budget.CategoryIsArchived ? "Yes" : "No");
        }

        return BuildFile($"budgets-{query.Year:D4}-{query.Month:D2}.csv", csv);
    }

    private static ExportFileDto BuildFile(string fileName, StringBuilder csv)
        => new(fileName, CsvContentType, new UTF8Encoding(false).GetBytes(csv.ToString()));

    private static void WriteRow(StringBuilder csv, params string[] values)
        => csv.AppendLine(string.Join(",", values.Select(Escape)));

    private static string Escape(string? value)
    {
        var normalized = value ?? string.Empty;
        var requiresQuotes = normalized.Contains(',') || normalized.Contains('"') || normalized.Contains('\n') || normalized.Contains('\r');
        if (!requiresQuotes)
        {
            return normalized;
        }

        return $"\"{normalized.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
