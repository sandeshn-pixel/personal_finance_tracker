namespace FinanceTracker.Application.Reports.DTOs;

public sealed record ReportSummaryDto(
    decimal TotalIncome,
    decimal TotalExpense,
    decimal NetCashFlow,
    int ExpenseTransactionCount,
    int IncomeTransactionCount);

public sealed record CategorySpendReportItemDto(Guid CategoryId, string CategoryName, decimal Amount);
public sealed record IncomeExpenseTrendPointDto(DateTime PeriodStartUtc, string Label, decimal Income, decimal Expense);
public sealed record AccountBalanceTrendPointDto(DateTime PeriodStartUtc, string Label, decimal Balance);

public sealed record ReportsOverviewDto(
    ReportSummaryDto Summary,
    IReadOnlyCollection<CategorySpendReportItemDto> CategorySpend,
    IReadOnlyCollection<IncomeExpenseTrendPointDto> IncomeExpenseTrend,
    IReadOnlyCollection<AccountBalanceTrendPointDto> AccountBalanceTrend);
