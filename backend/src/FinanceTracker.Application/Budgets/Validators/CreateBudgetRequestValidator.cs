using FinanceTracker.Application.Budgets.DTOs;
using FluentValidation;

namespace FinanceTracker.Application.Budgets.Validators;

public sealed class CreateBudgetRequestValidator : AbstractValidator<CreateBudgetRequest>
{
    public CreateBudgetRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.AlertThresholdPercent).InclusiveBetween(1, 100);
    }
}
