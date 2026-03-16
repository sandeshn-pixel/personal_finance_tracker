namespace FinanceTracker.Application.Automation.DTOs;

public sealed record AutomationRunSummaryDto(
    int UsersProcessed,
    int TransactionsCreated,
    int AutoOccurrencesProcessed,
    int ManualRemindersCreated,
    int GoalRemindersCreated,
    DateTime ProcessedAtUtc);