namespace FinanceTracker.Application.Settings.DTOs;

public sealed record SampleDataSeedStatusDto(
    bool CanSeedFromDashboard,
    bool CanRunSeed,
    bool HasTransactions,
    int ActiveAccountCount,
    int BudgetCount,
    int GoalCount,
    int RecurringRuleCount);
