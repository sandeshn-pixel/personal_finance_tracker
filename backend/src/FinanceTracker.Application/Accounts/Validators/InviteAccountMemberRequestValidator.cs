using FinanceTracker.Application.Accounts.DTOs;
using FinanceTracker.Domain.Enums;
using FluentValidation;

namespace FinanceTracker.Application.Accounts.Validators;

public sealed class InviteAccountMemberRequestValidator : AbstractValidator<InviteAccountMemberRequest>
{
    public InviteAccountMemberRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleFor(x => x.Role)
            .Must(role => role is AccountMemberRole.Editor or AccountMemberRole.Viewer)
            .WithMessage("Only editor or viewer members can be invited.");
    }
}
