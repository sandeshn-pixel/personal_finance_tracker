namespace FinanceTracker.Application.Budgets.Interfaces;

using FinanceTracker.Application.Budgets.DTOs;

public interface IBudgetService
{
    Task<IReadOnlyCollection<BudgetDto>> ListByMonthAsync(Guid userId, BudgetMonthQuery query, CancellationToken cancellationToken);
    Task<BudgetDto> CreateAsync(Guid userId, CreateBudgetRequest request, CancellationToken cancellationToken);
    Task<BudgetDto> UpdateAsync(Guid userId, Guid budgetId, UpdateBudgetRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, Guid budgetId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<BudgetDto>> CopyPreviousMonthAsync(Guid userId, CopyBudgetsRequest request, CancellationToken cancellationToken);
    Task<BudgetMonthSummaryDto> GetSummaryAsync(Guid userId, BudgetMonthQuery query, CancellationToken cancellationToken);
}
