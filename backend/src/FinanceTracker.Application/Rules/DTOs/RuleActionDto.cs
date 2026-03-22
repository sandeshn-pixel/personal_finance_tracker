namespace FinanceTracker.Application.Rules.DTOs;

public sealed record RuleActionDto(
    RuleActionType Type,
    Guid? CategoryId,
    string? Tag,
    string? AlertTitle,
    string? AlertMessage);
