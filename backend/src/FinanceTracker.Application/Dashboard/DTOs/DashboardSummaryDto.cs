using FinanceTracker.Application.Transactions.DTOs;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Dashboard.DTOs;

public sealed record CategorySpendDto(Guid CategoryId, string CategoryName, decimal Amount);
public sealed record TrendPointDto(string Label, decimal Income, decimal Expense);
public sealed record AccountBalanceSliceDto(Guid AccountId, string AccountName, string AccountType, string CurrencyCode, decimal CurrentBalance);
public sealed record GoalProgressDto(Guid GoalId, string GoalName, string? Icon, string? Color, decimal CurrentAmount, decimal TargetAmount, decimal ProgressPercent, string? LinkedAccountName, DateTime? TargetDateUtc, GoalStatus Status);
public sealed record BudgetUsageItemDto(Guid BudgetId, Guid CategoryId, string CategoryName, decimal Budgeted, decimal Spent, decimal Remaining, decimal UsagePercent, bool IsOverBudget, bool IsThresholdReached, bool CanManage, string OwnerDisplayName);
public sealed record BudgetHealthDto(
    decimal TotalBudgeted,
    decimal TotalSpent,
    decimal TotalRemaining,
    int OverBudgetCount,
    int ThresholdReachedCount,
    int SharedReadOnlyBudgetCount,
    int SharedOwnerCount);
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
    IReadOnlyCollection<TrendPointDto> IncomeExpenseTrend,
    IReadOnlyCollection<AccountBalanceSliceDto> AccountBalanceDistribution,
    IReadOnlyCollection<GoalProgressDto> GoalProgress,
    IReadOnlyCollection<BudgetUsageItemDto> BudgetUsage,
    BudgetHealthDto BudgetHealth,
    SavingsAutomationSummaryDto SavingsAutomation,
    IReadOnlyCollection<RecentGoalActivityDto> RecentGoalActivities);
