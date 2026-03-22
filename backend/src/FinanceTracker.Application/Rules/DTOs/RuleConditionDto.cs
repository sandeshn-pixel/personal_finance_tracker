using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Rules.DTOs;

public sealed record RuleConditionDto(
    RuleConditionField Field,
    RuleConditionOperator Operator,
    string? TextValue,
    decimal? AmountValue,
    Guid? CategoryId,
    Guid? AccountId,
    TransactionType? TransactionType);
