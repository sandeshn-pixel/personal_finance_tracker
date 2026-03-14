using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Categories.DTOs;

public sealed class CreateCategoryRequest
{
    public string Name { get; init; } = string.Empty;
    public CategoryType Type { get; init; }
}
