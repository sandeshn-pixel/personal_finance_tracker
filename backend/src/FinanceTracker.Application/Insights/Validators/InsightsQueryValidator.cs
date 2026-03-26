using FinanceTracker.Application.Insights.DTOs;
using FluentValidation;

namespace FinanceTracker.Application.Insights.Validators;

public sealed class InsightsQueryValidator : AbstractValidator<InsightsQuery>
{
    public InsightsQueryValidator()
    {
        RuleFor(x => x.StartDateUtc)
            .LessThanOrEqualTo(x => x.EndDateUtc);

        RuleFor(x => x)
            .Must(x => (x.EndDateUtc.Date - x.StartDateUtc.Date).TotalDays <= 366)
            .WithMessage("Insight range must not exceed 366 days.");

        RuleForEach(x => x.AccountIds)
            .Must(accountId => accountId != Guid.Empty)
            .WithMessage("Account filter contains an invalid value.");

        RuleForEach(x => x.CategoryIds)
            .Must(categoryId => categoryId != Guid.Empty)
            .WithMessage("Category filter contains an invalid value.");
    }
}
