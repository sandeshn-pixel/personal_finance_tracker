using FinanceTracker.Application.Budgets.DTOs;
using FinanceTracker.Application.Budgets.Interfaces;
using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Financial;

public sealed class BudgetService(ApplicationDbContext dbContext) : IBudgetService
{
    public async Task<IReadOnlyCollection<BudgetDto>> ListByMonthAsync(Guid userId, BudgetMonthQuery query, CancellationToken cancellationToken)
    {
        var budgets = await dbContext.Budgets
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Year == query.Year && x.Month == query.Month)
            .Include(x => x.Category)
            .OrderBy(x => x.Category.Name)
            .ToListAsync(cancellationToken);

        var actuals = await LoadActualsAsync(userId, query.Year, query.Month, cancellationToken);

        return budgets
            .Select(budget => MapBudget(budget, actuals.GetValueOrDefault(budget.CategoryId)))
            .ToList();
    }

    public async Task<BudgetDto> CreateAsync(Guid userId, CreateBudgetRequest request, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == request.CategoryId, cancellationToken)
            ?? throw new ValidationException("Selected category was not found.");

        if (category.IsArchived)
        {
            throw new ValidationException("Archived categories cannot receive new budgets.");
        }

        if (category.Type != CategoryType.Expense)
        {
            throw new ValidationException("Budgets can only be created for expense categories.");
        }

        var exists = await dbContext.Budgets.AnyAsync(
            x => x.UserId == userId && x.CategoryId == request.CategoryId && x.Year == request.Year && x.Month == request.Month,
            cancellationToken);

        if (exists)
        {
            throw new ConflictException("A budget for this category and month already exists.");
        }

        var budget = new Budget
        {
            UserId = userId,
            CategoryId = request.CategoryId,
            Year = request.Year,
            Month = request.Month,
            Amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
            AlertThresholdPercent = request.AlertThresholdPercent
        };

        dbContext.Budgets.Add(budget);
        await dbContext.SaveChangesAsync(cancellationToken);

        var actuals = await LoadActualsAsync(userId, request.Year, request.Month, cancellationToken);
        budget.Category = category;
        return MapBudget(budget, actuals.GetValueOrDefault(budget.CategoryId));
    }

    public async Task<BudgetDto> UpdateAsync(Guid userId, Guid budgetId, UpdateBudgetRequest request, CancellationToken cancellationToken)
    {
        var budget = await dbContext.Budgets
            .Include(x => x.Category)
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == budgetId, cancellationToken)
            ?? throw new NotFoundException("Budget was not found.");

        budget.Amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        budget.AlertThresholdPercent = request.AlertThresholdPercent;
        await dbContext.SaveChangesAsync(cancellationToken);

        var actuals = await LoadActualsAsync(userId, budget.Year, budget.Month, cancellationToken);
        return MapBudget(budget, actuals.GetValueOrDefault(budget.CategoryId));
    }

    public async Task DeleteAsync(Guid userId, Guid budgetId, CancellationToken cancellationToken)
    {
        var budget = await dbContext.Budgets
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == budgetId, cancellationToken)
            ?? throw new NotFoundException("Budget was not found.");

        dbContext.Budgets.Remove(budget);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<BudgetDto>> CopyPreviousMonthAsync(Guid userId, CopyBudgetsRequest request, CancellationToken cancellationToken)
    {
        var targetDate = new DateTime(request.Year, request.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var sourceDate = targetDate.AddMonths(-1);

        var sourceBudgets = await dbContext.Budgets
            .Include(x => x.Category)
            .Where(x => x.UserId == userId && x.Year == sourceDate.Year && x.Month == sourceDate.Month && !x.Category.IsArchived)
            .ToListAsync(cancellationToken);

        if (sourceBudgets.Count == 0)
        {
            return [];
        }

        var existingBudgets = await dbContext.Budgets
            .Where(x => x.UserId == userId && x.Year == request.Year && x.Month == request.Month)
            .ToListAsync(cancellationToken);

        var existingByCategory = existingBudgets.ToDictionary(x => x.CategoryId, x => x);

        foreach (var source in sourceBudgets)
        {
            if (existingByCategory.TryGetValue(source.CategoryId, out var existing))
            {
                if (!request.OverwriteExisting)
                {
                    continue;
                }

                existing.Amount = source.Amount;
                existing.AlertThresholdPercent = source.AlertThresholdPercent;
                continue;
            }

            dbContext.Budgets.Add(new Budget
            {
                UserId = userId,
                CategoryId = source.CategoryId,
                Year = request.Year,
                Month = request.Month,
                Amount = source.Amount,
                AlertThresholdPercent = source.AlertThresholdPercent
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await ListByMonthAsync(userId, new BudgetMonthQuery(request.Year, request.Month), cancellationToken);
    }

    public async Task<BudgetMonthSummaryDto> GetSummaryAsync(Guid userId, BudgetMonthQuery query, CancellationToken cancellationToken)
    {
        var budgets = await ListByMonthAsync(userId, query, cancellationToken);
        return new BudgetMonthSummaryDto(
            query.Year,
            query.Month,
            budgets.Sum(x => x.Amount),
            budgets.Sum(x => x.ActualSpent),
            budgets.Sum(x => x.Remaining),
            budgets.Count(x => x.IsOverBudget),
            budgets.Count(x => x.IsThresholdReached));
    }

    private async Task<Dictionary<Guid, decimal>> LoadActualsAsync(Guid userId, int year, int month, CancellationToken cancellationToken)
    {
        var baseQuery = dbContext.Transactions
            .AsNoTracking()
            .Where(x => x.UserId == userId && !x.IsDeleted && x.Type == TransactionType.Expense && x.CategoryId != null && x.DateUtc.Year == year && x.DateUtc.Month == month)
            .Select(x => new { CategoryId = x.CategoryId!.Value, x.Amount });

        if (string.Equals(dbContext.Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal))
        {
            var rows = await baseQuery.ToListAsync(cancellationToken);
            return rows
                .GroupBy(x => x.CategoryId)
                .ToDictionary(x => x.Key, x => x.Sum(item => item.Amount));
        }

        return await baseQuery
            .GroupBy(x => x.CategoryId)
            .Select(g => new { CategoryId = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Amount, cancellationToken);
    }

    private static BudgetDto MapBudget(Budget budget, decimal actualSpent)
    {
        var remaining = decimal.Round(budget.Amount - actualSpent, 2, MidpointRounding.AwayFromZero);
        var percentageUsed = budget.Amount == 0m
            ? 0m
            : decimal.Round((actualSpent / budget.Amount) * 100m, 2, MidpointRounding.AwayFromZero);
        var isOverBudget = actualSpent > budget.Amount;
        var isThresholdReached = percentageUsed >= budget.AlertThresholdPercent;

        return new BudgetDto(
            budget.Id,
            budget.CategoryId,
            budget.Category.Name,
            budget.Category.IsArchived,
            budget.Year,
            budget.Month,
            budget.Amount,
            budget.AlertThresholdPercent,
            actualSpent,
            remaining,
            percentageUsed,
            isOverBudget,
            isThresholdReached);
    }
}
