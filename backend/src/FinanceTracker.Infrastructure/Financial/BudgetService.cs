using FinanceTracker.Application.Budgets.DTOs;
using FinanceTracker.Application.Budgets.Interfaces;
using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Financial;

public sealed class BudgetService(
    ApplicationDbContext dbContext,
    AccountAccessService accountAccessService) : IBudgetService
{
    public async Task<IReadOnlyCollection<BudgetDto>> ListByMonthAsync(Guid userId, BudgetMonthQuery query, CancellationToken cancellationToken)
    {
        var sharedAccounts = await LoadAccessibleSharedAccountsAsync(userId, cancellationToken);
        var budgets = await LoadVisibleBudgetsAsync(userId, query.Year, query.Month, sharedAccounts, cancellationToken);
        var actuals = await LoadActualsAsync(userId, query.Year, query.Month, sharedAccounts, cancellationToken);

        return budgets
            .Select(budget => MapBudget(
                budget,
                actuals.GetValueOrDefault((budget.UserId, budget.CategoryId)),
                budget.UserId == userId))
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

        var actuals = await LoadActualsAsync(userId, request.Year, request.Month, [], cancellationToken);
        budget.Category = category;
        budget.User = await dbContext.Users.SingleAsync(x => x.Id == userId, cancellationToken);
        return MapBudget(budget, actuals.GetValueOrDefault((budget.UserId, budget.CategoryId)), true);
    }

    public async Task<BudgetDto> UpdateAsync(Guid userId, Guid budgetId, UpdateBudgetRequest request, CancellationToken cancellationToken)
    {
        var budget = await dbContext.Budgets
            .Include(x => x.Category)
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == budgetId, cancellationToken)
            ?? throw new NotFoundException("Budget was not found.");

        budget.Amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        budget.AlertThresholdPercent = request.AlertThresholdPercent;
        await dbContext.SaveChangesAsync(cancellationToken);

        var actuals = await LoadActualsAsync(userId, budget.Year, budget.Month, [], cancellationToken);
        return MapBudget(budget, actuals.GetValueOrDefault((budget.UserId, budget.CategoryId)), true);
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

    private async Task<List<Budget>> LoadVisibleBudgetsAsync(Guid userId, int year, int month, IReadOnlyCollection<(Guid AccountId, Guid OwnerUserId)> sharedAccounts, CancellationToken cancellationToken)
    {
        var ownBudgets = await dbContext.Budgets
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Year == year && x.Month == month)
            .Include(x => x.Category)
            .Include(x => x.User)
            .OrderBy(x => x.Category.Name)
            .ToListAsync(cancellationToken);

        if (sharedAccounts.Count == 0)
        {
            return ownBudgets;
        }

        var sharedAccountIds = sharedAccounts.Select(x => x.AccountId).Distinct().ToList();
        var sharedOwnerIds = sharedAccounts.Select(x => x.OwnerUserId).Distinct().ToList();

        var sharedBudgetIds = await dbContext.Transactions
            .AsNoTracking()
            .Where(x => !x.IsDeleted
                && x.Type == TransactionType.Expense
                && x.CategoryId != null
                && x.DateUtc.Year == year
                && x.DateUtc.Month == month
                && sharedAccountIds.Contains(x.AccountId)
                && sharedOwnerIds.Contains(x.UserId))
            .Select(x => new { x.UserId, CategoryId = x.CategoryId!.Value })
            .Distinct()
            .Join(
                dbContext.Budgets.AsNoTracking().Where(x => x.Year == year && x.Month == month && sharedOwnerIds.Contains(x.UserId)),
                transaction => new { transaction.UserId, transaction.CategoryId },
                budget => new { budget.UserId, budget.CategoryId },
                (_, budget) => budget.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (sharedBudgetIds.Count == 0)
        {
            return ownBudgets;
        }

        var sharedBudgets = await dbContext.Budgets
            .AsNoTracking()
            .Where(x => sharedBudgetIds.Contains(x.Id))
            .Include(x => x.Category)
            .Include(x => x.User)
            .ToListAsync(cancellationToken);

        return ownBudgets
            .Concat(sharedBudgets)
            .OrderBy(x => x.UserId == userId ? 0 : 1)
            .ThenBy(x => BuildDisplayName(x.User))
            .ThenBy(x => x.Category.Name)
            .ToList();
    }

    private async Task<Dictionary<(Guid UserId, Guid CategoryId), decimal>> LoadActualsAsync(
        Guid userId,
        int year,
        int month,
        IReadOnlyCollection<(Guid AccountId, Guid OwnerUserId)> sharedAccounts,
        CancellationToken cancellationToken)
    {
        var actuals = new Dictionary<(Guid UserId, Guid CategoryId), decimal>();

        var ownRows = await dbContext.Transactions
            .AsNoTracking()
            .Where(x => x.UserId == userId
                && !x.IsDeleted
                && x.Type == TransactionType.Expense
                && x.CategoryId != null
                && x.DateUtc.Year == year
                && x.DateUtc.Month == month)
            .Select(x => new { x.UserId, CategoryId = x.CategoryId!.Value, x.Amount })
            .ToListAsync(cancellationToken);

        foreach (var row in ownRows)
        {
            var key = (row.UserId, row.CategoryId);
            actuals[key] = actuals.GetValueOrDefault(key) + row.Amount;
        }

        if (sharedAccounts.Count == 0)
        {
            return actuals;
        }

        var sharedAccountIds = sharedAccounts.Select(x => x.AccountId).Distinct().ToList();
        var sharedOwnerIds = sharedAccounts.Select(x => x.OwnerUserId).Distinct().ToList();

        var sharedRows = await dbContext.Transactions
            .AsNoTracking()
            .Where(x => !x.IsDeleted
                && x.Type == TransactionType.Expense
                && x.CategoryId != null
                && x.DateUtc.Year == year
                && x.DateUtc.Month == month
                && sharedAccountIds.Contains(x.AccountId)
                && sharedOwnerIds.Contains(x.UserId))
            .Select(x => new { x.UserId, CategoryId = x.CategoryId!.Value, x.Amount })
            .ToListAsync(cancellationToken);

        foreach (var row in sharedRows)
        {
            var key = (row.UserId, row.CategoryId);
            actuals[key] = actuals.GetValueOrDefault(key) + row.Amount;
        }

        return actuals;
    }

    private async Task<IReadOnlyCollection<(Guid AccountId, Guid OwnerUserId)>> LoadAccessibleSharedAccountsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var accounts = await accountAccessService.QueryAccessibleAccounts(userId, AccountMemberRole.Viewer, includeArchived: true)
            .Where(x => x.UserId != userId)
            .Select(x => new { x.Id, x.UserId })
            .ToListAsync(cancellationToken);

        return accounts
            .Select(x => (x.Id, x.UserId))
            .Distinct()
            .ToList();
    }

    private static BudgetDto MapBudget(Budget budget, decimal actualSpent, bool canManage)
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
            isThresholdReached,
            canManage,
            BuildDisplayName(budget.User));
    }

    private static string BuildDisplayName(User user)
    {
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }
}
