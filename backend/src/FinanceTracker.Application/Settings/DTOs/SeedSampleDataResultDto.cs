namespace FinanceTracker.Application.Settings.DTOs;

public sealed record SeedSampleDataResultDto(
    string Message,
    int AccountsCreated,
    int TransactionsCreated,
    int BudgetsCreated,
    int GoalsCreated,
    int RecurringRulesCreated);
