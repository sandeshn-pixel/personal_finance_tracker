namespace FinanceTracker.Application.RecurringTransactions.DTOs;

public sealed record RecurringExecutionSummaryDto(
    int RulesVisited,
    int TransactionsCreated,
    int OccurrencesProcessed,
    int OccurrencesSkipped,
    DateTime ProcessedAtUtc);