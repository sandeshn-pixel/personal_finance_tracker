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
    private const string PdfContentType = "application/pdf";

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

        return BuildFile($"transactions-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv", CsvContentType, new UTF8Encoding(false).GetBytes(csv.ToString()));
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

        WriteRow(csv, "Comparison", "Previous total income", overview.Comparison.PreviousTotalIncome.ToString("0.00", CultureInfo.InvariantCulture));
        WriteRow(csv, "Comparison", "Previous total expense", overview.Comparison.PreviousTotalExpense.ToString("0.00", CultureInfo.InvariantCulture));
        WriteRow(csv, "Comparison", "Previous net cash flow", overview.Comparison.PreviousNetCashFlow.ToString("0.00", CultureInfo.InvariantCulture));
        csv.AppendLine();

        WriteRow(csv, "CategorySpend", "Category", "Amount");
        foreach (var item in overview.CategorySpend)
        {
            WriteRow(csv, "CategorySpend", item.CategoryName, item.Amount.ToString("0.00", CultureInfo.InvariantCulture));
        }

        csv.AppendLine();
        WriteRow(csv, "TopMerchants", "Merchant", "Amount", "TransactionCount");
        foreach (var item in overview.TopMerchants)
        {
            WriteRow(csv, "TopMerchants", item.MerchantName, item.Amount.ToString("0.00", CultureInfo.InvariantCulture), item.TransactionCount.ToString(CultureInfo.InvariantCulture));
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

        return BuildFile($"reports-overview-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv", CsvContentType, new UTF8Encoding(false).GetBytes(csv.ToString()));
    }

    public async Task<ExportFileDto> ExportReportOverviewPdfAsync(Guid userId, ReportQuery query, CancellationToken cancellationToken)
    {
        var overview = await reportService.GetOverviewAsync(userId, query, cancellationToken);
        var lines = BuildReportPdfLines(query, overview);
        var pdf = BuildSimplePdf(lines);
        return BuildFile($"reports-overview-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf", PdfContentType, pdf);
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

        return BuildFile($"budgets-{query.Year:D4}-{query.Month:D2}.csv", CsvContentType, new UTF8Encoding(false).GetBytes(csv.ToString()));
    }

    private static IReadOnlyList<PdfLine> BuildReportPdfLines(ReportQuery query, ReportsOverviewDto overview)
    {
        var lines = new List<PdfLine>
        {
            new("Personal Finance Tracker - Reports Overview", 18, true),
            new($"Range: {query.StartDateUtc:dd MMM yyyy} to {query.EndDateUtc:dd MMM yyyy}", 10),
            new(string.Empty, 6),
            new("Summary", 14, true),
            new($"Total income: {FormatMoney(overview.Summary.TotalIncome)}", 11),
            new($"Total expense: {FormatMoney(overview.Summary.TotalExpense)}", 11),
            new($"Net cash flow: {FormatMoney(overview.Summary.NetCashFlow)}", 11),
            new($"Income transactions: {overview.Summary.IncomeTransactionCount}", 11),
            new($"Expense transactions: {overview.Summary.ExpenseTransactionCount}", 11),
            new(string.Empty, 4),
            new("Previous Period Comparison", 14, true),
            new($"Previous income: {FormatMoney(overview.Comparison.PreviousTotalIncome)}", 11),
            new($"Previous expense: {FormatMoney(overview.Comparison.PreviousTotalExpense)}", 11),
            new($"Previous net cash flow: {FormatMoney(overview.Comparison.PreviousNetCashFlow)}", 11),
            new(string.Empty, 4),
            new("Category Spend", 14, true)
        };

        lines.AddRange(overview.CategorySpend.Take(8).Select(item => new PdfLine($"- {item.CategoryName}: {FormatMoney(item.Amount)}", 11)));
        lines.Add(new(string.Empty, 4));
        lines.Add(new("Top Merchants", 14, true));
        lines.AddRange((overview.TopMerchants.Count == 0
            ? [new PdfLine("No merchant data available for this range.", 11)]
            : overview.TopMerchants.Select(item => new PdfLine($"- {item.MerchantName}: {FormatMoney(item.Amount)} across {item.TransactionCount} transaction{(item.TransactionCount == 1 ? string.Empty : "s")}", 11))).Take(8));
        lines.Add(new(string.Empty, 4));
        lines.Add(new("Income vs Expense Trend", 14, true));
        lines.AddRange(overview.IncomeExpenseTrend.Take(10).Select(point => new PdfLine($"- {point.Label}: income {FormatMoney(point.Income)}, expense {FormatMoney(point.Expense)}", 11)));
        lines.Add(new(string.Empty, 4));
        lines.Add(new("Account Balance Trend", 14, true));
        lines.AddRange(overview.AccountBalanceTrend.Take(10).Select(point => new PdfLine($"- {point.Label}: {FormatMoney(point.Balance)}", 11)));

        return lines;
    }

    private static byte[] BuildSimplePdf(IReadOnlyList<PdfLine> lines)
    {
        const decimal pageWidth = 612m;
        const decimal pageHeight = 792m;
        const decimal marginLeft = 50m;
        const decimal marginTop = 48m;
        const decimal defaultLeading = 16m;
        const int maxLinesPerPage = 42;

        var pages = new List<List<PdfLine>>();
        var currentPage = new List<PdfLine>();
        var effectiveLineCount = 0m;

        foreach (var line in lines)
        {
            var weight = line.FontSize >= 14 ? 1.6m : line.FontSize <= 6 ? 0.6m : 1m;
            if (currentPage.Count > 0 && effectiveLineCount + weight > maxLinesPerPage)
            {
                pages.Add(currentPage);
                currentPage = new List<PdfLine>();
                effectiveLineCount = 0m;
            }

            currentPage.Add(line);
            effectiveLineCount += weight;
        }

        if (currentPage.Count > 0)
        {
            pages.Add(currentPage);
        }

        var objects = new List<string>();
        var fontRegularId = AddObject(objects, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        var fontBoldId = AddObject(objects, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");

        var pageObjectIds = new List<int>();
        foreach (var pageLines in pages)
        {
            var content = new StringBuilder();
            content.AppendLine("BT");
            var y = pageHeight - marginTop;
            foreach (var line in pageLines)
            {
                var fontName = line.IsBold ? "F2" : "F1";
                content.AppendLine($"/{fontName} {line.FontSize.ToString("0.##", CultureInfo.InvariantCulture)} Tf");
                content.AppendLine($"1 0 0 1 {marginLeft.ToString("0.##", CultureInfo.InvariantCulture)} {y.ToString("0.##", CultureInfo.InvariantCulture)} Tm");
                content.AppendLine($"({EscapePdfText(line.Text)}) Tj");
                y -= line.FontSize >= 14 ? defaultLeading + 2 : line.FontSize <= 6 ? 8 : defaultLeading;
            }
            content.AppendLine("ET");

            var contentBytes = Encoding.ASCII.GetBytes(content.ToString());
            var contentId = AddObject(objects, $"<< /Length {contentBytes.Length} >>\nstream\n{content}\nendstream");
            var pageId = AddObject(objects, $"<< /Type /Page /Parent {{PAGES}} 0 R /MediaBox [0 0 {pageWidth.ToString(CultureInfo.InvariantCulture)} {pageHeight.ToString(CultureInfo.InvariantCulture)}] /Resources << /Font << /F1 {fontRegularId} 0 R /F2 {fontBoldId} 0 R >> >> /Contents {contentId} 0 R >>");
            pageObjectIds.Add(pageId);
        }

        var kids = string.Join(" ", pageObjectIds.Select(id => $"{id} 0 R"));
        var pagesId = AddObject(objects, $"<< /Type /Pages /Kids [{kids}] /Count {pageObjectIds.Count} >>");
        for (var i = 0; i < objects.Count; i++)
        {
            objects[i] = objects[i].Replace("{PAGES}", pagesId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        var catalogId = AddObject(objects, $"<< /Type /Catalog /Pages {pagesId} 0 R >>");
        var output = new StringBuilder();
        output.AppendLine("%PDF-1.4");
        var offsets = new List<int> { 0 };

        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(output.ToString()));
            output.AppendLine($"{i + 1} 0 obj");
            output.AppendLine(objects[i]);
            output.AppendLine("endobj");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(output.ToString());
        output.AppendLine("xref");
        output.AppendLine($"0 {objects.Count + 1}");
        output.AppendLine("0000000000 65535 f ");
        foreach (var offset in offsets.Skip(1))
        {
            output.AppendLine($"{offset:D10} 00000 n ");
        }

        output.AppendLine("trailer");
        output.AppendLine($"<< /Size {objects.Count + 1} /Root {catalogId} 0 R >>");
        output.AppendLine("startxref");
        output.AppendLine(xrefOffset.ToString(CultureInfo.InvariantCulture));
        output.AppendLine("%%EOF");
        return Encoding.ASCII.GetBytes(output.ToString());
    }

    private static int AddObject(ICollection<string> objects, string content)
    {
        objects.Add(content);
        return objects.Count;
    }

    private static string EscapePdfText(string value)
        => (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);

    private static string FormatMoney(decimal amount)
        => amount.ToString("C", CultureInfo.CreateSpecificCulture("en-IN"));

    private static ExportFileDto BuildFile(string fileName, string contentType, byte[] content)
        => new(fileName, contentType, content);

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

    private sealed record PdfLine(string Text, decimal FontSize, bool IsBold = false);
}
