using FinanceTracker.Application.Rules.DTOs;

namespace FinanceTracker.Application.Rules.Interfaces;

public interface IRuleService
{
    Task<IReadOnlyCollection<TransactionRuleDto>> ListAsync(Guid userId, CancellationToken cancellationToken);
    Task<TransactionRuleDto> CreateAsync(Guid userId, UpsertTransactionRuleRequest request, CancellationToken cancellationToken);
    Task<TransactionRuleDto> UpdateAsync(Guid userId, Guid ruleId, UpsertTransactionRuleRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken);
}
