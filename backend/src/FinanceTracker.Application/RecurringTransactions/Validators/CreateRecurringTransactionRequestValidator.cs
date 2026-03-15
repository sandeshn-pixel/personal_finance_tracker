using FinanceTracker.Application.RecurringTransactions.DTOs;
using FinanceTracker.Domain.Enums;
using FluentValidation;

namespace FinanceTracker.Application.RecurringTransactions.Validators;

public sealed class CreateRecurringTransactionRequestValidator : AbstractValidator<CreateRecurringTransactionRequest>
{
    public CreateRecurringTransactionRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.StartDateUtc)
            .Must(date => date.Kind == DateTimeKind.Utc)
            .WithMessage("Start date must be expressed in UTC.");
        RuleFor(x => x.EndDateUtc)
            .Must(date => !date.HasValue || date.Value.Kind == DateTimeKind.Utc)
            .WithMessage("End date must be expressed in UTC.");
        RuleFor(x => x.EndDateUtc)
            .GreaterThanOrEqualTo(x => x.StartDateUtc)
            .When(x => x.EndDateUtc.HasValue);
        RuleFor(x => x.TransferAccountId)
            .NotNull()
            .When(x => x.Type == TransactionType.Transfer)
            .WithMessage("Transfer rules require a destination account.");
        RuleFor(x => x.CategoryId)
            .NotNull()
            .When(x => x.Type != TransactionType.Transfer)
            .WithMessage("Income and expense rules require a category.");
    }
}