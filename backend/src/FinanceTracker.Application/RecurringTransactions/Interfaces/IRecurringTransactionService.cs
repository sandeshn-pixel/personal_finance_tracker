using FinanceTracker.Application.RecurringTransactions.DTOs;

namespace FinanceTracker.Application.RecurringTransactions.Interfaces;

public interface IRecurringTransactionService
{
    Task<IReadOnlyCollection<RecurringTransactionDto>> ListAsync(Guid userId, CancellationToken cancellationToken);
    Task<RecurringTransactionDto?> GetAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken);
    Task<RecurringTransactionDto> CreateAsync(Guid userId, CreateRecurringTransactionRequest request, CancellationToken cancellationToken);
    Task<RecurringTransactionDto> UpdateAsync(Guid userId, Guid ruleId, UpdateRecurringTransactionRequest request, CancellationToken cancellationToken);
    Task<RecurringTransactionDto> PauseAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken);
    Task<RecurringTransactionDto> ResumeAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, Guid ruleId, CancellationToken cancellationToken);
    Task<RecurringExecutionSummaryDto> ProcessDueAsync(Guid userId, DateTime asOfUtc, CancellationToken cancellationToken);
}