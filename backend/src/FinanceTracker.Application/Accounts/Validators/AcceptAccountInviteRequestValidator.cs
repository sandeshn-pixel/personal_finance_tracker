using FinanceTracker.Application.Accounts.DTOs;
using FluentValidation;

namespace FinanceTracker.Application.Accounts.Validators;

public sealed class AcceptAccountInviteRequestValidator : AbstractValidator<AcceptAccountInviteRequest>
{
    public AcceptAccountInviteRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .MaximumLength(512);
    }
}
