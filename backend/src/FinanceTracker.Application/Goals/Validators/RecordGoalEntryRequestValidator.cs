using FinanceTracker.Application.Goals.DTOs;
using FluentValidation;

namespace FinanceTracker.Application.Goals.Validators;

public sealed class RecordGoalEntryRequestValidator : AbstractValidator<RecordGoalEntryRequest>
{
    public RecordGoalEntryRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.OccurredAtUtc)
            .Must(date => !date.HasValue || date.Value.Kind == DateTimeKind.Utc)
            .WithMessage("Entry date must be expressed in UTC.");
        RuleFor(x => x.Note).MaximumLength(240);
    }
}