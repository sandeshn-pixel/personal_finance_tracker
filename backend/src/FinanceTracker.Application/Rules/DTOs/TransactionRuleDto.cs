namespace FinanceTracker.Application.Rules.DTOs;

public sealed record TransactionRuleDto(
    Guid Id,
    string Name,
    int Priority,
    bool IsActive,
    RuleConditionDto Condition,
    RuleActionDto Action,
    string ConditionSummary,
    string ActionSummary,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);
