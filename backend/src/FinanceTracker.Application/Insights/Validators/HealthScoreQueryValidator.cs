using FinanceTracker.Application.Insights.DTOs;
using FluentValidation;

namespace FinanceTracker.Application.Insights.Validators;

public sealed class HealthScoreQueryValidator : AbstractValidator<HealthScoreQuery>
{
    public HealthScoreQueryValidator()
    {
        RuleFor(x => x.AccountId)
            .Must(accountId => !accountId.HasValue || accountId.Value != Guid.Empty)
            .WithMessage("Account filter is invalid.");

        RuleForEach(x => x.AccountIds)
            .Must(accountId => accountId != Guid.Empty)
            .WithMessage("Account filter contains an invalid value.");
    }
}
