using FluentValidation;
using FinanceTracker.Application.Transactions.DTOs;

namespace FinanceTracker.Application.Transactions.Validators;

public sealed class TransactionListQueryValidator : AbstractValidator<TransactionListQuery>
{
    public TransactionListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.MinAmount).GreaterThanOrEqualTo(0m).When(x => x.MinAmount.HasValue);
        RuleFor(x => x.MaxAmount).GreaterThanOrEqualTo(0m).When(x => x.MaxAmount.HasValue);
        RuleFor(x => x).Must(x => !x.StartDateUtc.HasValue || !x.EndDateUtc.HasValue || x.StartDateUtc <= x.EndDateUtc)
            .WithMessage("Start date must be earlier than or equal to end date.");
        RuleFor(x => x).Must(x => !x.MinAmount.HasValue || !x.MaxAmount.HasValue || x.MinAmount <= x.MaxAmount)
            .WithMessage("Minimum amount must be less than or equal to maximum amount.");
        RuleFor(x => x.Search).MaximumLength(120);
    }
}
