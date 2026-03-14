using FinanceTracker.Application.Common;
using FinanceTracker.Application.Categories.DTOs;
using FinanceTracker.Application.Categories.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Financial;

public sealed class CategorySeeder(ApplicationDbContext dbContext) : ICategorySeeder
{
    private static readonly (string Name, CategoryType Type)[] Defaults =
    [
        ("Food", CategoryType.Expense),
        ("Rent", CategoryType.Expense),
        ("Utilities", CategoryType.Expense),
        ("Transport", CategoryType.Expense),
        ("Entertainment", CategoryType.Expense),
        ("Shopping", CategoryType.Expense),
        ("Health", CategoryType.Expense),
        ("Education", CategoryType.Expense),
        ("Travel", CategoryType.Expense),
        ("Subscriptions", CategoryType.Expense),
        ("Miscellaneous", CategoryType.Expense),
        ("Salary", CategoryType.Income),
        ("Freelance", CategoryType.Income),
        ("Bonus", CategoryType.Income),
        ("Investment", CategoryType.Income),
        ("Gift", CategoryType.Income),
        ("Refund", CategoryType.Income),
        ("Other", CategoryType.Income)
    ];

    public Task EnsureDefaultsAsync(User user, CancellationToken cancellationToken) => EnsureDefaultsInternalAsync(user.Id, cancellationToken);
    public Task EnsureDefaultsAsync(Guid userId, CancellationToken cancellationToken) => EnsureDefaultsInternalAsync(userId, cancellationToken);

    private async Task EnsureDefaultsInternalAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Categories
            .Where(x => x.UserId == userId)
            .Select(x => new { x.Name, x.Type })
            .ToListAsync(cancellationToken);

        foreach (var item in Defaults)
        {
            var exists = existing.Any(x => x.Type == item.Type && string.Equals(x.Name, item.Name, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                continue;
            }

            dbContext.Categories.Add(new Category
            {
                UserId = userId,
                Name = item.Name,
                Type = item.Type,
                IsSystem = true,
                IsArchived = false
            });
        }
    }
}
