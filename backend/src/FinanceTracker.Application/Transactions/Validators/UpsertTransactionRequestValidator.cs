using FluentValidation;
using FinanceTracker.Application.Transactions.DTOs;

namespace FinanceTracker.Application.Transactions.Validators;

public sealed class UpsertTransactionRequestValidator : AbstractValidator<UpsertTransactionRequest>
{
    public UpsertTransactionRequestValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m).LessThanOrEqualTo(999999999999.99m);
        RuleFor(x => x.DateUtc).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.Merchant).MaximumLength(120);
        RuleFor(x => x.PaymentMethod).MaximumLength(50);
        RuleFor(x => x.Tags).Must(tags => tags.Count <= 10).WithMessage("A maximum of 10 tags is allowed.");
    }
}
