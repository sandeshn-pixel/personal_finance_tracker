namespace FinanceTracker.Application.Rules.DTOs;

public sealed record UpsertTransactionRuleRequest(
    string Name,
    int Priority,
    bool IsActive,
    RuleConditionDto Condition,
    RuleActionDto Action);
