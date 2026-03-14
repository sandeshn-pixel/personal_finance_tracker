using FluentValidation;
using FinanceTracker.Application.Accounts.DTOs;

namespace FinanceTracker.Application.Accounts.Validators;

public sealed class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3).Matches("^[A-Z]{3}$");
        RuleFor(x => x.OpeningBalance).InclusiveBetween(-999999999999.99m, 999999999999.99m);
        RuleFor(x => x.InstitutionName).MaximumLength(120);
        RuleFor(x => x.Last4Digits).MaximumLength(4).Matches("^$|^[0-9]{4}$");
    }
}
