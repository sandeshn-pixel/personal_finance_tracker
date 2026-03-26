using FinanceTracker.Application.Reports.DTOs;
using FluentValidation;

namespace FinanceTracker.Application.Reports.Validators;

public sealed class ReportTrendsQueryValidator : AbstractValidator<ReportTrendsQuery>
{
    public ReportTrendsQueryValidator()
    {
        RuleFor(x => x.StartDateUtc)
            .LessThanOrEqualTo(x => x.EndDateUtc);

        RuleFor(x => x)
            .Must(x => (x.EndDateUtc.Date - x.StartDateUtc.Date).TotalDays <= 366)
            .WithMessage("Reporting range must not exceed 366 days.");

        RuleForEach(x => x.AccountIds)
            .Must(accountId => accountId != Guid.Empty)
            .WithMessage("Account filter contains an invalid value.");

        RuleForEach(x => x.CategoryIds)
            .Must(categoryId => categoryId != Guid.Empty)
            .WithMessage("Category filter contains an invalid value.");
    }
}
