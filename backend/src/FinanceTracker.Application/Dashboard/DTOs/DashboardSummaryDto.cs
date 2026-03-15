using FinanceTracker.Application.Transactions.DTOs;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Dashboard.DTOs;

public sealed record CategorySpendDto(Guid CategoryId, string CategoryName, decimal Amount);
public sealed record BudgetHealthDto(
    decimal TotalBudgeted,
    decimal TotalSpent,
    decimal TotalRemaining,
    int OverBudgetCount,
    int ThresholdReachedCount);
public sealed record SavingsAutomationSummaryDto(
    decimal TotalContributedToGoals,
    decimal TotalWithdrawnFromGoals,
    decimal NetGoalSavings,
    int ActiveGoalsCount,
    int CompletedGoalsCount,
    int ActiveRecurringRulesCount,
    int PausedRecurringRulesCount,
    int DueRecurringRulesCount);
public sealed record RecentGoalActivityDto(
    Guid Id,
    Guid GoalId,
    string GoalName,
    GoalEntryType Type,
    decimal Amount,
    DateTime OccurredAtUtc,
    string? Note,
    string? AccountName);

public sealed record DashboardSummaryDto(
    decimal CurrentMonthIncome,
    decimal CurrentMonthExpense,
    decimal NetBalance,
    IReadOnlyCollection<TransactionDto> RecentTransactions,
    IReadOnlyCollection<CategorySpendDto> SpendingByCategory,
    BudgetHealthDto BudgetHealth,
    SavingsAutomationSummaryDto SavingsAutomation,
    IReadOnlyCollection<RecentGoalActivityDto> RecentGoalActivities);