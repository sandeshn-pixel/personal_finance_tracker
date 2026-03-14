using FinanceTracker.Application.Transactions.DTOs;

namespace FinanceTracker.Application.Dashboard.DTOs;

public sealed record CategorySpendDto(Guid CategoryId, string CategoryName, decimal Amount);
public sealed record BudgetHealthDto(
    decimal TotalBudgeted,
    decimal TotalSpent,
    decimal TotalRemaining,
    int OverBudgetCount,
    int ThresholdReachedCount);

public sealed record DashboardSummaryDto(
    decimal CurrentMonthIncome,
    decimal CurrentMonthExpense,
    decimal NetBalance,
    IReadOnlyCollection<TransactionDto> RecentTransactions,
    IReadOnlyCollection<CategorySpendDto> SpendingByCategory,
    BudgetHealthDto BudgetHealth);
