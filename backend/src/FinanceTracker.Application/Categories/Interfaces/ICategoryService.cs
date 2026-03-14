using FinanceTracker.Application.Categories.DTOs;
using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Categories.Interfaces;

public interface ICategoryService
{
    Task<IReadOnlyCollection<CategoryDto>> ListAsync(Guid userId, bool includeArchived, CancellationToken cancellationToken);
    Task<CategoryDto> CreateAsync(Guid userId, CreateCategoryRequest request, CancellationToken cancellationToken);
    Task<CategoryDto> UpdateAsync(Guid userId, Guid categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken);
    Task ArchiveAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken);
}

public interface ICategorySeeder
{
    Task EnsureDefaultsAsync(Guid userId, CancellationToken cancellationToken);
    Task EnsureDefaultsAsync(User user, CancellationToken cancellationToken);
}
