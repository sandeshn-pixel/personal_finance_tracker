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

    private static readonly PdfColor DarkInk = PdfColor.FromHex("4B2E2B");
    private static readonly PdfColor MutedInk = PdfColor.FromHex("6E625B");
    private static readonly PdfColor Accent = PdfColor.FromHex("C08552");
    private static readonly PdfColor AccentStrong = PdfColor.FromHex("8C5A3C");
    private static readonly PdfColor Surface = PdfColor.FromHex("FFF8F0");
    private static readonly PdfColor Border = PdfColor.FromHex("E7DDD4");
    private static readonly PdfColor Success = PdfColor.FromHex("1F6A46");
    private static readonly PdfColor Danger = PdfColor.FromHex("8B3A32");

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
        var pdf = BuildAdvancedOverviewPdf(query, overview);
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

    private static byte[] BuildAdvancedOverviewPdf(ReportQuery query, ReportsOverviewDto overview)
    {
        var document = new PdfDocumentBuilder();
        var pageOne = document.AddPage();
        var pageTwo = document.AddPage();

        DrawOverviewCover(pageOne, query, overview);
        DrawTrendPage(pageTwo, query, overview);

        return document.Build();
    }

    private static void DrawOverviewCover(PdfPageBuilder page, ReportQuery query, ReportsOverviewDto overview)
    {
        page.FillRect(36m, 710m, 540m, 50m, DarkInk);
        page.DrawText(52m, 737m, "Ledger Nest", 10m, true, Surface);
        page.DrawText(52m, 718m, "Reports Overview", 19m, true, Surface);
        page.DrawText(410m, 736m, "Generated", 8.5m, false, Surface);
        page.DrawText(410m, 721m, DateTime.UtcNow.ToString("dd MMM yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture), 9.5m, true, Surface);

        DrawExecutiveSummary(page, 40m, 688m, 532m, 76m, query, overview);

        var summaryCards = new[]
        {
            new PdfMetricCard("Total income", FormatMoney(overview.Summary.TotalIncome), $"{overview.Summary.IncomeTransactionCount} recorded inflow{(overview.Summary.IncomeTransactionCount == 1 ? string.Empty : "s")}", Success),
            new PdfMetricCard("Total expense", FormatMoney(overview.Summary.TotalExpense), $"{overview.Summary.ExpenseTransactionCount} recorded outflow{(overview.Summary.ExpenseTransactionCount == 1 ? string.Empty : "s")}", Danger),
            new PdfMetricCard("Net cash flow", FormatMoney(overview.Summary.NetCashFlow), overview.Summary.NetCashFlow >= 0 ? "Cash flow stayed positive in this range" : "Outflows exceeded inflows in this range", overview.Summary.NetCashFlow >= 0 ? AccentStrong : Danger),
            new PdfMetricCard("Coverage", $"{overview.CategorySpend.Count} categories", $"{overview.TopMerchants.Count} merchants surfaced", DarkInk),
        };
        DrawMetricCards(page, 40m, 592m, 126m, 72m, 9.33m, summaryCards);

        page.DrawSectionTitle(40m, 486m, "Previous Period Comparison");
        var comparisonCards = new[]
        {
            new PdfMetricCard("Income vs previous", FormatMoney(overview.Comparison.PreviousTotalIncome), FormatDelta(overview.Summary.TotalIncome, overview.Comparison.PreviousTotalIncome), Success),
            new PdfMetricCard("Expense vs previous", FormatMoney(overview.Comparison.PreviousTotalExpense), FormatDelta(overview.Summary.TotalExpense, overview.Comparison.PreviousTotalExpense), Danger),
            new PdfMetricCard("Net vs previous", FormatMoney(overview.Comparison.PreviousNetCashFlow), FormatDelta(overview.Summary.NetCashFlow, overview.Comparison.PreviousNetCashFlow), overview.Summary.NetCashFlow >= overview.Comparison.PreviousNetCashFlow ? AccentStrong : Danger),
        };
        DrawMetricCards(page, 40m, 456m, 172m, 58m, 12m, comparisonCards);

        page.DrawSectionTitle(40m, 376m, "What stood out");
        var highlightCards = BuildHighlightCards(overview);
        DrawMetricCards(page, 40m, 346m, 172m, 62m, 12m, highlightCards);

        DrawDataTable(
            page,
            40m,
            182m,
            256m,
            "Category spend",
            new[] { 158m, 78m },
            new[] { "Category", "Amount" },
            ToCategoryRows(overview.CategorySpend));

        DrawDataTable(
            page,
            316m,
            182m,
            256m,
            "Top merchants",
            new[] { 136m, 62m, 38m },
            new[] { "Merchant", "Amount", "Count" },
            ToMerchantRows(overview.TopMerchants));

        page.DrawFooter("Amounts are shown in INR. Transfers are excluded from income and expense comparisons in this export.");
    }

    private static void DrawTrendPage(PdfPageBuilder page, ReportQuery query, ReportsOverviewDto overview)
    {
        page.FillRect(36m, 710m, 540m, 50m, AccentStrong);
        page.DrawText(52m, 736m, "Trend Snapshot", 19m, true, Surface);
        page.DrawText(52m, 718m, $"{query.StartDateUtc:dd MMM yyyy} to {query.EndDateUtc:dd MMM yyyy}", 9.5m, false, Surface);

        DrawIncomeExpenseTrendPanel(page, 40m, 678m, 532m, 238m, overview.IncomeExpenseTrend);
        DrawBalanceTrendPanel(page, 40m, 404m, 532m, 192m, overview.AccountBalanceTrend);

        page.DrawWrappedText(
            40m,
            178m,
            532m,
            9.2m,
            MutedInk,
            "Use this export for review and sharing. For filtering, trend exploration, and interactive comparisons, use the in-app Reports and Insights views.",
            false,
            12m,
            3);
        page.DrawFooter("Generated from persisted finance data only. Exported summaries do not change account balances, budgets, or ledger history.");
    }

    private static PdfMetricCard[] BuildHighlightCards(ReportsOverviewDto overview)
    {
        var topCategory = overview.CategorySpend.OrderByDescending(item => item.Amount).FirstOrDefault();
        var topMerchant = overview.TopMerchants.OrderByDescending(item => item.Amount).FirstOrDefault();
        var strongestPeriod = overview.IncomeExpenseTrend
            .Select(item => new { item.Label, Net = item.Income - item.Expense })
            .OrderByDescending(item => item.Net)
            .FirstOrDefault();

        return new[]
        {
            new PdfMetricCard(
                "Top category",
                topCategory is null ? "No expense data" : FitPdfText(topCategory.CategoryName, 104m, 13m),
                topCategory is null ? "No category spend available" : FormatMoney(topCategory.Amount),
                AccentStrong),
            new PdfMetricCard(
                "Top merchant",
                topMerchant is null ? "No merchant data" : FitPdfText(topMerchant.MerchantName, 104m, 13m),
                topMerchant is null ? "No merchant spend available" : $"{FormatMoney(topMerchant.Amount)} across {topMerchant.TransactionCount} txns",
                DarkInk),
            new PdfMetricCard(
                "Best period",
                strongestPeriod is null ? "No trend data" : strongestPeriod.Label,
                strongestPeriod is null ? "Add more history to compare periods" : $"Net {FormatMoney(strongestPeriod.Net)}",
                strongestPeriod is not null && strongestPeriod.Net >= 0m ? Success : Danger),
        };
    }

    private static void DrawExecutiveSummary(PdfPageBuilder page, decimal x, decimal topY, decimal width, decimal height, ReportQuery query, ReportsOverviewDto overview)
    {
        var bottomY = topY - height;
        page.FillRect(x, bottomY, width, height, Surface);
        page.StrokeRect(x, bottomY, width, height, Border, 0.8m);
        page.DrawText(x + 16m, topY - 18m, "Executive summary", 9m, true, MutedInk);
        page.DrawText(x + 16m, topY - 38m, $"Period: {query.StartDateUtc:dd MMM yyyy} to {query.EndDateUtc:dd MMM yyyy}", 10.5m, true, DarkInk);
        page.DrawText(x + 16m, topY - 54m, $"Scope: {DescribeScope(query)}", 9.2m, false, MutedInk);

        var summaryText = BuildExecutiveSummaryText(overview);
        page.DrawWrappedText(x + 248m, topY - 24m, width - 264m, 10m, DarkInk, summaryText, false, 13m, 4);
    }

    private static string BuildExecutiveSummaryText(ReportsOverviewDto overview)
    {
        var topCategory = overview.CategorySpend.OrderByDescending(item => item.Amount).FirstOrDefault();
        var topCategoryText = topCategory is null
            ? "No category concentration stood out in this period."
            : $"Highest spend came from {topCategory.CategoryName} at {FormatMoney(topCategory.Amount)}.";

        var netText = overview.Summary.NetCashFlow >= 0m
            ? $"Net cash flow stayed positive at {FormatMoney(overview.Summary.NetCashFlow)}."
            : $"Net cash flow ended negative at {FormatMoney(overview.Summary.NetCashFlow)}.";

        return netText + " " + topCategoryText;
    }

    private static void DrawIncomeExpenseTrendPanel(PdfPageBuilder page, decimal x, decimal topY, decimal width, decimal height, IReadOnlyCollection<IncomeExpenseTrendPointDto> trend)
    {
        DrawPanelShell(page, x, topY, width, height, "Income vs expense trend", "Each row compares period income and expense on the same scale.");

        var rows = trend.Take(6).ToList();
        if (rows.Count == 0)
        {
            page.DrawText(x + 18m, topY - 72m, "No trend data available for this period.", 10m, false, MutedInk);
            return;
        }

        var maxValue = rows.SelectMany(item => new[] { item.Income, item.Expense }).DefaultIfEmpty(1m).Max();
        var rowTop = topY - 70m;
        var chartLeft = x + 118m;
        var chartWidth = 274m;

        foreach (var row in rows)
        {
            page.DrawText(x + 18m, rowTop - 4m, row.Label, 9m, true, DarkInk);
            page.DrawText(x + 18m, rowTop - 17m, $"Net {FormatMoney(row.Income - row.Expense)}", 8.2m, false, MutedInk);

            page.FillRect(chartLeft, rowTop - 11m, chartWidth, 7m, Border.WithAlpha(0.35m));
            page.FillRect(chartLeft, rowTop - 25m, chartWidth, 7m, Border.WithAlpha(0.35m));

            var incomeWidth = maxValue == 0m ? 0m : chartWidth * (row.Income / maxValue);
            var expenseWidth = maxValue == 0m ? 0m : chartWidth * (row.Expense / maxValue);
            page.FillRect(chartLeft, rowTop - 11m, incomeWidth, 7m, Success);
            page.FillRect(chartLeft, rowTop - 25m, expenseWidth, 7m, Danger);

            page.DrawText(chartLeft + chartWidth + 10m, rowTop - 6m, FormatMoney(row.Income), 8.5m, true, Success);
            page.DrawText(chartLeft + chartWidth + 10m, rowTop - 20m, FormatMoney(row.Expense), 8.5m, true, Danger);
            rowTop -= 32m;
        }

        page.DrawText(x + 18m, topY - height + 18m, "Income", 8.5m, true, Success);
        page.DrawText(x + 64m, topY - height + 18m, "Expense", 8.5m, true, Danger);
    }

    private static void DrawBalanceTrendPanel(PdfPageBuilder page, decimal x, decimal topY, decimal width, decimal height, IReadOnlyCollection<AccountBalanceTrendPointDto> trend)
    {
        DrawPanelShell(page, x, topY, width, height, "Balance checkpoints", "Balance snapshots across the selected reporting range.");

        var rows = trend.Take(7).ToList();
        if (rows.Count == 0)
        {
            page.DrawText(x + 18m, topY - 72m, "No balance checkpoints available.", 10m, false, MutedInk);
            return;
        }

        var maxAbs = rows.Select(item => Math.Abs(item.Balance)).DefaultIfEmpty(1m).Max();
        var rowTop = topY - 66m;
        var chartLeft = x + 138m;
        var chartWidth = 278m;

        foreach (var row in rows)
        {
            var barWidth = maxAbs == 0m ? 0m : chartWidth * (Math.Abs(row.Balance) / maxAbs);
            var color = row.Balance >= 0m ? AccentStrong : Danger;

            page.DrawText(x + 18m, rowTop - 3m, row.Label, 8.8m, true, DarkInk);
            page.FillRect(chartLeft, rowTop - 10m, chartWidth, 8m, Border.WithAlpha(0.35m));
            page.FillRect(chartLeft, rowTop - 10m, barWidth, 8m, color);
            page.DrawText(chartLeft + chartWidth + 10m, rowTop - 4m, FormatMoney(row.Balance), 8.5m, true, color);
            rowTop -= 22m;
        }
    }

    private static void DrawPanelShell(PdfPageBuilder page, decimal x, decimal topY, decimal width, decimal height, string title, string subtitle)
    {
        var bottomY = topY - height;
        page.FillRect(x, bottomY, width, height, Surface);
        page.StrokeRect(x, bottomY, width, height, Border, 0.8m);
        page.DrawText(x + 18m, topY - 20m, title, 12m, true, DarkInk);
        page.DrawWrappedText(x + 18m, topY - 38m, width - 36m, 8.7m, MutedInk, subtitle, false, 11m, 2);
    }

    private static void DrawMetricCards(PdfPageBuilder page, decimal x, decimal topY, decimal cardWidth, decimal cardHeight, decimal gap, IReadOnlyList<PdfMetricCard> cards)
    {
        for (var index = 0; index < cards.Count; index++)
        {
            var left = x + index * (cardWidth + gap);
            DrawMetricCard(page, left, topY, cardWidth, cardHeight, cards[index]);
        }
    }

    private static void DrawMetricCard(PdfPageBuilder page, decimal x, decimal topY, decimal width, decimal height, PdfMetricCard card)
    {
        var bottomY = topY - height;
        page.FillRect(x, bottomY, width, height, Surface);
        page.StrokeRect(x, bottomY, width, height, Border, 0.8m);
        page.DrawText(x + 12m, topY - 17m, card.Label, 8.5m, false, MutedInk);
        page.DrawText(x + 12m, topY - 38m, card.Value, 15m, true, card.ValueColor);
        page.DrawWrappedText(x + 12m, topY - 52m, width - 24m, 8.5m, MutedInk, card.Hint, false, 11m, 2);
    }

    private static void DrawDataTable(PdfPageBuilder page, decimal x, decimal topY, decimal width, string title, decimal[] columnWidths, string[] headers, IReadOnlyList<string[]> rows)
    {
        page.DrawSectionTitle(x, topY + 22m, title);

        var rowHeight = 22m;
        var headerTopY = topY;
        var tableHeight = rowHeight * (rows.Count + 1);
        var bottomY = headerTopY - tableHeight;

        page.FillRect(x, bottomY, width, tableHeight, Surface);
        page.StrokeRect(x, bottomY, width, tableHeight, Border, 0.8m);
        page.FillRect(x, headerTopY - rowHeight, width, rowHeight, Border.WithAlpha(0.42m));

        decimal runningX = x;
        for (var index = 0; index < headers.Length; index++)
        {
            page.DrawText(runningX + 8m, headerTopY - 15m, headers[index], 8.5m, true, MutedInk);
            if (index < headers.Length - 1)
            {
                page.DrawLine(runningX + columnWidths[index], bottomY, runningX + columnWidths[index], headerTopY, Border, 0.5m);
            }
            runningX += columnWidths[index];
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var rowTop = headerTopY - rowHeight * (rowIndex + 1);
            if (rowIndex < rows.Count - 1)
            {
                page.DrawLine(x, rowTop - rowHeight, x + width, rowTop - rowHeight, Border.WithAlpha(0.7m), 0.5m);
            }

            decimal cellX = x;
            for (var columnIndex = 0; columnIndex < headers.Length; columnIndex++)
            {
                var alignedRight = columnIndex > 0;
                var value = FitPdfText(rows[rowIndex][columnIndex], columnWidths[columnIndex] - 16m, 8.8m);
                if (alignedRight)
                {
                    var widthEstimate = EstimateTextWidth(value, 8.8m);
                    page.DrawText(cellX + columnWidths[columnIndex] - widthEstimate - 8m, rowTop - 15m, value, 8.8m, false, DarkInk);
                }
                else
                {
                    page.DrawText(cellX + 8m, rowTop - 15m, value, 8.8m, false, DarkInk);
                }
                cellX += columnWidths[columnIndex];
            }
        }
    }

    private static IReadOnlyList<string[]> ToCategoryRows(IReadOnlyCollection<CategorySpendReportItemDto> categorySpend)
    {
        var rows = categorySpend.Take(7)
            .Select(item => new[] { item.CategoryName, FormatMoney(item.Amount) })
            .ToList();

        return rows.Count == 0
            ? [new[] { "No category spend in range", "-" }]
            : rows;
    }

    private static IReadOnlyList<string[]> ToMerchantRows(IReadOnlyCollection<MerchantSpendReportItemDto> merchants)
    {
        var rows = merchants.Take(7)
            .Select(item => new[]
            {
                item.MerchantName,
                FormatMoney(item.Amount),
                item.TransactionCount.ToString(CultureInfo.InvariantCulture)
            })
            .ToList();

        return rows.Count == 0
            ? [new[] { "No merchant data", "-", "-" }]
            : rows;
    }

    private static IReadOnlyList<string[]> ToIncomeExpenseRows(IReadOnlyCollection<IncomeExpenseTrendPointDto> trend)
    {
        var rows = trend.Take(9)
            .Select(item => new[]
            {
                item.Label,
                FormatMoney(item.Income),
                FormatMoney(item.Expense),
                FormatMoney(item.Income - item.Expense)
            })
            .ToList();

        return rows.Count == 0
            ? [new[] { "No trend data", "-", "-", "-" }]
            : rows;
    }

    private static IReadOnlyList<string[]> ToBalanceRows(IReadOnlyCollection<AccountBalanceTrendPointDto> trend)
    {
        var rows = trend.Take(10)
            .Select(item => new[]
            {
                item.Label,
                FormatMoney(item.Balance)
            })
            .ToList();

        return rows.Count == 0
            ? [new[] { "No balance checkpoints", "-" }]
            : rows;
    }

    private static string DescribeScope(ReportQuery query)
    {
        if (query.AccountId.HasValue)
        {
            return "Single selected account";
        }

        if (query.AccountIds is { Length: > 0 })
        {
            return $"{query.AccountIds.Length} scoped account{(query.AccountIds.Length == 1 ? string.Empty : "s")}";
        }

        return "All accessible accounts";
    }

    private static string FormatDelta(decimal current, decimal previous)
    {
        if (previous == 0m)
        {
            return current == 0m ? "No change vs previous period" : "No prior-period baseline";
        }

        var delta = current - previous;
        var percentage = Math.Abs((delta / previous) * 100m);
        return delta >= 0m
            ? $"Up {percentage:0.#}% vs previous"
            : $"Down {percentage:0.#}% vs previous";
    }

    private static string FitPdfText(string value, decimal maxWidth, decimal fontSize)
    {
        var candidate = value ?? string.Empty;
        if (EstimateTextWidth(candidate, fontSize) <= maxWidth)
        {
            return candidate;
        }

        const string ellipsis = "...";
        while (candidate.Length > 1 && EstimateTextWidth(candidate + ellipsis, fontSize) > maxWidth)
        {
            candidate = candidate[..^1];
        }

        return candidate + ellipsis;
    }

    private static decimal EstimateTextWidth(string value, decimal fontSize)
        => (value?.Length ?? 0) * fontSize * 0.52m;

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

    private sealed record PdfMetricCard(string Label, string Value, string Hint, PdfColor ValueColor);

    private readonly record struct PdfColor(decimal R, decimal G, decimal B, decimal Alpha = 1m)
    {
        public static PdfColor FromHex(string hex)
        {
            var normalized = hex.TrimStart('#');
            var r = Convert.ToInt32(normalized[..2], 16) / 255m;
            var g = Convert.ToInt32(normalized.Substring(2, 2), 16) / 255m;
            var b = Convert.ToInt32(normalized.Substring(4, 2), 16) / 255m;
            return new PdfColor(r, g, b);
        }

        public PdfColor WithAlpha(decimal alpha) => this with { Alpha = alpha };
    }

    private sealed class PdfDocumentBuilder
    {
        private readonly List<PdfPageBuilder> pages = [];

        public PdfPageBuilder AddPage()
        {
            var page = new PdfPageBuilder();
            pages.Add(page);
            return page;
        }

        public byte[] Build()
        {
            var objects = new List<string>();
            var fontRegularId = AddObject(objects, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
            var fontBoldId = AddObject(objects, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");

            var pageObjectIds = new List<int>();
            foreach (var page in pages)
            {
                var content = page.Build();
                var contentBytes = Encoding.ASCII.GetBytes(content);
                var contentId = AddObject(objects, $"<< /Length {contentBytes.Length} >>\nstream\n{content}\nendstream");
                var pageId = AddObject(objects, $"<< /Type /Page /Parent {{PAGES}} 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 {fontRegularId} 0 R /F2 {fontBoldId} 0 R >> >> /Contents {contentId} 0 R >>");
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
    }

    private sealed class PdfPageBuilder
    {
        private readonly StringBuilder commands = new();

        public void FillRect(decimal x, decimal y, decimal width, decimal height, PdfColor color)
        {
            commands.AppendLine("q");
            commands.AppendLine($"{FormatColor(color)} rg");
            commands.AppendLine($"{F(x)} {F(y)} {F(width)} {F(height)} re f");
            commands.AppendLine("Q");
        }

        public void StrokeRect(decimal x, decimal y, decimal width, decimal height, PdfColor color, decimal lineWidth)
        {
            commands.AppendLine("q");
            commands.AppendLine($"{F(lineWidth)} w");
            commands.AppendLine($"{FormatColor(color)} RG");
            commands.AppendLine($"{F(x)} {F(y)} {F(width)} {F(height)} re S");
            commands.AppendLine("Q");
        }

        public void DrawLine(decimal x1, decimal y1, decimal x2, decimal y2, PdfColor color, decimal lineWidth)
        {
            commands.AppendLine("q");
            commands.AppendLine($"{F(lineWidth)} w");
            commands.AppendLine($"{FormatColor(color)} RG");
            commands.AppendLine($"{F(x1)} {F(y1)} m {F(x2)} {F(y2)} l S");
            commands.AppendLine("Q");
        }

        public void DrawText(decimal x, decimal y, string text, decimal fontSize, bool bold, PdfColor color)
        {
            commands.AppendLine("BT");
            commands.AppendLine($"/{(bold ? "F2" : "F1")} {F(fontSize)} Tf");
            commands.AppendLine($"{FormatColor(color)} rg");
            commands.AppendLine($"1 0 0 1 {F(x)} {F(y)} Tm");
            commands.AppendLine($"({EscapePdfText(text)}) Tj");
            commands.AppendLine("ET");
        }

        public decimal DrawWrappedText(decimal x, decimal topY, decimal width, decimal fontSize, PdfColor color, string text, bool bold, decimal leading, int maxLines = 4)
        {
            var words = (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return topY;
            }

            var lines = new List<string>();
            var currentLine = words[0];
            for (var i = 1; i < words.Length; i++)
            {
                var candidate = currentLine + " " + words[i];
                if (EstimateTextWidth(candidate, fontSize) <= width)
                {
                    currentLine = candidate;
                }
                else
                {
                    lines.Add(currentLine);
                    currentLine = words[i];
                    if (lines.Count == maxLines - 1)
                    {
                        break;
                    }
                }
            }
            lines.Add(currentLine);

            if (lines.Count > maxLines)
            {
                lines = lines.Take(maxLines).ToList();
            }

            for (var i = 0; i < lines.Count; i++)
            {
                var value = i == maxLines - 1 && i < words.Length - 1 ? FitPdfText(lines[i], width, fontSize) : lines[i];
                DrawText(x, topY - i * leading, value, fontSize, bold, color);
            }

            return topY - lines.Count * leading;
        }

        public void DrawSectionTitle(decimal x, decimal y, string title)
        {
            DrawText(x, y, title, 12m, true, DarkInk);
            DrawLine(x, y - 6m, x + 160m, y - 6m, Border, 0.7m);
        }

        public void DrawFooter(string text)
        {
            DrawLine(40m, 56m, 572m, 56m, Border, 0.7m);
            DrawText(40m, 40m, FitPdfText(text, 528m, 8.5m), 8.5m, false, MutedInk);
        }

        public string Build() => commands.ToString();

        private static string F(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static string FormatColor(PdfColor color)
            => $"{F(color.R)} {F(color.G)} {F(color.B)}";
    }
}
