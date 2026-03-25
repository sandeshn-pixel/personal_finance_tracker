using FinanceTracker.Application.Dashboard.DTOs;
using FluentValidation;

namespace FinanceTracker.Application.Dashboard.Validators;

public sealed class DashboardQueryValidator : AbstractValidator<DashboardQuery>
{
    public DashboardQueryValidator()
    {
        RuleFor(x => x.AccountId)
            .Must(accountId => !accountId.HasValue || accountId.Value != Guid.Empty)
            .WithMessage("Account filter is invalid.");

        RuleForEach(x => x.AccountIds)
            .Must(accountId => accountId != Guid.Empty)
            .WithMessage("Account filter contains an invalid value.");
    }
}
