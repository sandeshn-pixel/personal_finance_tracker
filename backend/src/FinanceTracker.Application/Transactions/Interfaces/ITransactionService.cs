using FinanceTracker.Application.Common;
using FinanceTracker.Application.Transactions.DTOs;

namespace FinanceTracker.Application.Transactions.Interfaces;

public interface ITransactionService
{
    Task<PagedResult<TransactionDto>> ListAsync(Guid userId, TransactionListQuery query, CancellationToken cancellationToken);
    Task<TransactionDto?> GetAsync(Guid userId, Guid transactionId, CancellationToken cancellationToken);
    Task<TransactionDto> CreateAsync(Guid userId, UpsertTransactionRequest request, CancellationToken cancellationToken);
    Task<TransactionDto> UpdateAsync(Guid userId, Guid transactionId, UpsertTransactionRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, Guid transactionId, CancellationToken cancellationToken);
}
