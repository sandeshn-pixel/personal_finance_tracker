using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Forecasting.DTOs;

public sealed record ForecastRecurringItemDto(
    DateTime ScheduledDateUtc,
    string Title,
    TransactionType Type,
    decimal Amount,
    string AccountName);
