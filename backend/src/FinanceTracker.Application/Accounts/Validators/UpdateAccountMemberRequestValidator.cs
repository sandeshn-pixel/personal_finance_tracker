using FinanceTracker.Application.Accounts.DTOs;
using FinanceTracker.Domain.Enums;
using FluentValidation;

namespace FinanceTracker.Application.Accounts.Validators;

public sealed class UpdateAccountMemberRequestValidator : AbstractValidator<UpdateAccountMemberRequest>
{
    public UpdateAccountMemberRequestValidator()
    {
        RuleFor(x => x.Role)
            .Must(role => role is AccountMemberRole.Editor or AccountMemberRole.Viewer)
            .WithMessage("Members can only be changed to editor or viewer.");
    }
}
