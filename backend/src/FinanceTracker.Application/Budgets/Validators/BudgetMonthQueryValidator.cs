using FinanceTracker.Application.Budgets.DTOs;
using FluentValidation;

namespace FinanceTracker.Application.Budgets.Validators;

public sealed class BudgetMonthQueryValidator : AbstractValidator<BudgetMonthQuery>
{
    public BudgetMonthQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}
