namespace FinanceTracker.Application.Reports.DTOs;

public sealed record ReportSummaryDto(
    decimal TotalIncome,
    decimal TotalExpense,
    decimal NetCashFlow,
    int ExpenseTransactionCount,
    int IncomeTransactionCount);

public sealed record ReportPeriodComparisonDto(
    decimal PreviousTotalIncome,
    decimal PreviousTotalExpense,
    decimal PreviousNetCashFlow,
    int PreviousExpenseTransactionCount,
    int PreviousIncomeTransactionCount);

public sealed record CategorySpendReportItemDto(Guid CategoryId, string CategoryName, decimal Amount);
public sealed record MerchantSpendReportItemDto(string MerchantName, decimal Amount, int TransactionCount);
public sealed record IncomeExpenseTrendPointDto(DateTime PeriodStartUtc, string Label, decimal Income, decimal Expense);
public sealed record AccountBalanceTrendPointDto(DateTime PeriodStartUtc, string Label, decimal Balance);

public sealed record ReportsOverviewDto(
    ReportSummaryDto Summary,
    ReportPeriodComparisonDto Comparison,
    IReadOnlyCollection<CategorySpendReportItemDto> CategorySpend,
    IReadOnlyCollection<MerchantSpendReportItemDto> TopMerchants,
    IReadOnlyCollection<IncomeExpenseTrendPointDto> IncomeExpenseTrend,
    IReadOnlyCollection<AccountBalanceTrendPointDto> AccountBalanceTrend);
