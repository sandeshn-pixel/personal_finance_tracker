using FinanceTracker.Application.Common;
using FinanceTracker.Application.Categories.DTOs;
using FinanceTracker.Application.Categories.Interfaces;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Financial;

public sealed class CategoryService(ApplicationDbContext dbContext, ICategorySeeder categorySeeder) : ICategoryService
{
    public async Task<IReadOnlyCollection<CategoryDto>> ListAsync(Guid userId, bool includeArchived, CancellationToken cancellationToken)
    {
        await categorySeeder.EnsureDefaultsAsync(userId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await dbContext.Categories
            .AsNoTracking()
            .Where(x => x.UserId == userId && (includeArchived || !x.IsArchived))
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Name)
            .Select(x => new CategoryDto(x.Id, x.Name, x.Type, x.IsSystem, x.IsArchived))
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDto> CreateAsync(Guid userId, CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        await categorySeeder.EnsureDefaultsAsync(userId, cancellationToken);

        var normalizedName = request.Name.Trim();
        var exists = await dbContext.Categories.AnyAsync(x => x.UserId == userId && x.Type == request.Type && x.Name.ToLower() == normalizedName.ToLower(), cancellationToken);
        if (exists)
        {
            throw new ConflictException("A category with this name already exists for the selected type.");
        }

        var category = new Domain.Entities.Category
        {
            UserId = userId,
            Name = normalizedName,
            Type = request.Type,
            IsSystem = false,
            IsArchived = false
        };

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CategoryDto(category.Id, category.Name, category.Type, category.IsSystem, category.IsArchived);
    }

    public async Task<CategoryDto> UpdateAsync(Guid userId, Guid categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == categoryId, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");

        var normalizedName = request.Name.Trim();
        var exists = await dbContext.Categories.AnyAsync(x => x.UserId == userId && x.Type == category.Type && x.Id != categoryId && x.Name.ToLower() == normalizedName.ToLower(), cancellationToken);
        if (exists)
        {
            throw new ConflictException("A category with this name already exists for the selected type.");
        }

        category.Name = normalizedName;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CategoryDto(category.Id, category.Name, category.Type, category.IsSystem, category.IsArchived);
    }

    public async Task ArchiveAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == categoryId, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");

        category.IsArchived = true;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
