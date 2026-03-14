using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Categories.DTOs;

public sealed record CategoryDto(Guid Id, string Name, CategoryType Type, bool IsSystem, bool IsArchived);
