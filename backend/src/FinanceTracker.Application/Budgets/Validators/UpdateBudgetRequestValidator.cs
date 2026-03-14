using FinanceTracker.Application.Budgets.DTOs;
using FluentValidation;

namespace FinanceTracker.Application.Budgets.Validators;

public sealed class UpdateBudgetRequestValidator : AbstractValidator<UpdateBudgetRequest>
{
    public UpdateBudgetRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.AlertThresholdPercent).InclusiveBetween(1, 100);
    }
}
