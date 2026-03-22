using FluentValidation;
using FinanceTracker.Application.Rules.DTOs;

namespace FinanceTracker.Application.Rules.Validators;

public sealed class UpsertTransactionRuleRequestValidator : AbstractValidator<UpsertTransactionRuleRequest>
{
    public UpsertTransactionRuleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(80);

        RuleFor(x => x.Priority)
            .InclusiveBetween(1, 999);

        RuleFor(x => x.Condition)
            .NotNull()
            .SetValidator(new RuleConditionDtoValidator());

        RuleFor(x => x.Action)
            .NotNull()
            .SetValidator(new RuleActionDtoValidator());
    }
}

internal sealed class RuleConditionDtoValidator : AbstractValidator<RuleConditionDto>
{
    public RuleConditionDtoValidator()
    {
        RuleFor(x => x).Must(BeValidCombination).WithMessage("Condition field, operator, and value combination is invalid.");
    }

    private static bool BeValidCombination(RuleConditionDto condition)
        => condition.Field switch
        {
            RuleConditionField.Merchant =>
                condition.Operator is RuleConditionOperator.Equals or RuleConditionOperator.Contains
                && !string.IsNullOrWhiteSpace(condition.TextValue)
                && condition.TextValue.Length <= 120,
            RuleConditionField.Amount =>
                condition.Operator is RuleConditionOperator.GreaterThan or RuleConditionOperator.LessThan
                && condition.AmountValue.HasValue
                && condition.AmountValue.Value > 0m,
            RuleConditionField.Category =>
                condition.Operator == RuleConditionOperator.Equals
                && condition.CategoryId.HasValue,
            RuleConditionField.TransactionType =>
                condition.Operator == RuleConditionOperator.Equals
                && condition.TransactionType.HasValue,
            RuleConditionField.Account =>
                condition.Operator == RuleConditionOperator.Equals
                && condition.AccountId.HasValue,
            _ => false
        };
}

internal sealed class RuleActionDtoValidator : AbstractValidator<RuleActionDto>
{
    public RuleActionDtoValidator()
    {
        RuleFor(x => x).Must(BeValidAction).WithMessage("Action configuration is invalid.");
    }

    private static bool BeValidAction(RuleActionDto action)
        => action.Type switch
        {
            RuleActionType.SetCategory => action.CategoryId.HasValue,
            RuleActionType.AddTag => !string.IsNullOrWhiteSpace(action.Tag) && action.Tag.Trim().Length <= 40,
            RuleActionType.CreateAlert =>
                !string.IsNullOrWhiteSpace(action.AlertTitle)
                && action.AlertTitle.Trim().Length <= 120
                && !string.IsNullOrWhiteSpace(action.AlertMessage)
                && action.AlertMessage.Trim().Length <= 320,
            _ => false
        };
}
