using FinanceTracker.Application.Goals.DTOs;
using FluentValidation;

namespace FinanceTracker.Application.Goals.Validators;

public sealed class CreateGoalRequestValidator : AbstractValidator<CreateGoalRequest>
{
    public CreateGoalRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.TargetAmount).GreaterThan(0m);
        RuleFor(x => x.TargetDateUtc)
            .Must(date => !date.HasValue || date.Value.Kind == DateTimeKind.Utc)
            .WithMessage("Target date must be expressed in UTC.");
        RuleFor(x => x.Icon).MaximumLength(64);
        RuleFor(x => x.Color).MaximumLength(32);
    }
}