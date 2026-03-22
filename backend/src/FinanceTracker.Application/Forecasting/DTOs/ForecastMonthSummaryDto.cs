namespace FinanceTracker.Application.Forecasting.DTOs;

public sealed record ForecastMonthSummaryDto(
    decimal CurrentBalance,
    decimal ProjectedEndOfMonthBalance,
    decimal MinimumProjectedBalance,
    decimal SafeToSpend,
    decimal AverageDailyIncome,
    decimal AverageDailyExpense,
    decimal AverageDailyNet,
    int DaysRemainingInMonth,
    bool HasSparseData,
    ForecastRiskLevel RiskLevel,
    string BasisDescription,
    ForecastRecurringSummaryDto UpcomingRecurring,
    IReadOnlyCollection<string> Notes);
