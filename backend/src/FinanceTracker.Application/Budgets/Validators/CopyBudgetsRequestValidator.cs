using FinanceTracker.Application.Budgets.DTOs;
using FluentValidation;

namespace FinanceTracker.Application.Budgets.Validators;

public sealed class CopyBudgetsRequestValidator : AbstractValidator<CopyBudgetsRequest>
{
    public CopyBudgetsRequestValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}
