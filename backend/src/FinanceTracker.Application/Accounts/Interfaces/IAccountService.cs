using FinanceTracker.Application.Accounts.DTOs;

namespace FinanceTracker.Application.Accounts.Interfaces;

public interface IAccountService
{
    Task<IReadOnlyCollection<AccountDto>> ListAsync(Guid userId, bool includeArchived, CancellationToken cancellationToken);
    Task<AccountDto?> GetAsync(Guid userId, Guid accountId, CancellationToken cancellationToken);
    Task<AccountDto> CreateAsync(Guid userId, CreateAccountRequest request, CancellationToken cancellationToken);
    Task<AccountDto> UpdateAsync(Guid userId, Guid accountId, UpdateAccountRequest request, CancellationToken cancellationToken);
    Task ArchiveAsync(Guid userId, Guid accountId, CancellationToken cancellationToken);
}
