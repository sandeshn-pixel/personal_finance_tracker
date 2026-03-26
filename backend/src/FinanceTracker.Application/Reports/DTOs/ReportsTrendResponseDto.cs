namespace FinanceTracker.Application.Reports.DTOs;

public sealed record SavingsRateTrendPointDto(
    DateTime PeriodStartUtc,
    string Label,
    decimal Income,
    decimal Expense,
    decimal NetSavings,
    decimal? SavingsRatePercent,
    bool HasIncomeData);

public sealed record CategoryTrendPointDto(
    DateTime PeriodStartUtc,
    string Label,
    decimal Amount);

public sealed record CategoryTrendSeriesDto(
    Guid CategoryId,
    string CategoryName,
    decimal TotalAmount,
    IReadOnlyCollection<CategoryTrendPointDto> Points);

public sealed record ReportsTrendResponseDto(
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    ReportTimeBucket Bucket,
    bool HasSparseData,
    string BasisDescription,
    IReadOnlyCollection<IncomeExpenseTrendPointDto> IncomeExpenseTrend,
    IReadOnlyCollection<SavingsRateTrendPointDto> SavingsRateTrend,
    IReadOnlyCollection<CategoryTrendSeriesDto> CategoryTrends);
