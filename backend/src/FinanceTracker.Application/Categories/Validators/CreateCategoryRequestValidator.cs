using FluentValidation;
using FinanceTracker.Application.Categories.DTOs;

namespace FinanceTracker.Application.Categories.Validators;

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
    }
}
