using FinanceTracker.Application.Rules.DTOs;
using FinanceTracker.Application.Transactions.DTOs;

namespace FinanceTracker.Application.Rules.Interfaces;

public interface ITransactionRuleEvaluator
{
    Task<RuleEvaluationResult> EvaluateAsync(Guid userId, UpsertTransactionRequest request, CancellationToken cancellationToken);
    Task PublishAlertsAsync(Guid userId, Guid transactionId, IEnumerable<RuleEvaluationAlert> alerts, CancellationToken cancellationToken);
}

public sealed record RuleEvaluationAlert(Guid RuleId, string RuleName, string Title, string Message);

public sealed record RuleEvaluationResult(
    UpsertTransactionRequest Request,
    IReadOnlyCollection<Guid> MatchedRuleIds,
    IReadOnlyCollection<RuleEvaluationAlert> Alerts);
