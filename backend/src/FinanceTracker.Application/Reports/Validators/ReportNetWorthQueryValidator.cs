using FinanceTracker.Application.Reports.DTOs;
using FluentValidation;

namespace FinanceTracker.Application.Reports.Validators;

public sealed class ReportNetWorthQueryValidator : AbstractValidator<ReportNetWorthQuery>
{
    public ReportNetWorthQueryValidator()
    {
        RuleFor(x => x.StartDateUtc)
            .LessThanOrEqualTo(x => x.EndDateUtc);

        RuleFor(x => x)
            .Must(x => (x.EndDateUtc.Date - x.StartDateUtc.Date).TotalDays <= 366)
            .WithMessage("Reporting range must not exceed 366 days.");

        RuleForEach(x => x.AccountIds)
            .Must(accountId => accountId != Guid.Empty)
            .WithMessage("Account filter contains an invalid value.");
    }
}
