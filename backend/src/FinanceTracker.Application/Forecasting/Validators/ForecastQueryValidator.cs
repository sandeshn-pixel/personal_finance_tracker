using FinanceTracker.Application.Forecasting.DTOs;
using FluentValidation;

namespace FinanceTracker.Application.Forecasting.Validators;

public sealed class ForecastQueryValidator : AbstractValidator<ForecastQuery>
{
    public ForecastQueryValidator()
    {
        RuleFor(x => x.AccountId)
            .Must(accountId => !accountId.HasValue || accountId.Value != Guid.Empty)
            .WithMessage("Account filter is invalid.");

        RuleForEach(x => x.AccountIds)
            .Must(accountId => accountId != Guid.Empty)
            .WithMessage("Account filter contains an invalid value.");
    }
}
