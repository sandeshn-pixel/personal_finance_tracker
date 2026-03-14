using FluentValidation;
using FinanceTracker.Application.Categories.DTOs;

namespace FinanceTracker.Application.Categories.Validators;

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
    }
}
