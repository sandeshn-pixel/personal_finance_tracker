using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Financial;

public sealed class AccountAccessService(ApplicationDbContext dbContext)
{
    public IQueryable<Account> QueryAccessibleAccounts(Guid userId, AccountMemberRole minimumRole, bool includeArchived)
    {
        var query = dbContext.Accounts
            .AsQueryable()
            .WhereUserHasMinimumRole(userId, minimumRole);

        if (!includeArchived)
        {
            query = query.Where(x => !x.IsArchived);
        }

        return query;
    }

    public Task<Account?> FindAccessibleAccountAsync(Guid userId, Guid accountId, AccountMemberRole minimumRole, bool includeArchived, CancellationToken cancellationToken)
    {
        return QueryAccessibleAccounts(userId, minimumRole, includeArchived)
            .SingleOrDefaultAsync(x => x.Id == accountId, cancellationToken);
    }

    public async Task<Account> RequireOwnedAccountAsync(Guid userId, Guid accountId, bool includeArchived, CancellationToken cancellationToken)
    {
        var query = dbContext.Accounts.AsQueryable().Where(x => x.UserId == userId);
        if (!includeArchived)
        {
            query = query.Where(x => !x.IsArchived);
        }

        return await query.SingleOrDefaultAsync(x => x.Id == accountId, cancellationToken)
            ?? throw new NotFoundException("Account was not found.");
    }

    public async Task<Account> RequireAccessibleAccountAsync(Guid userId, Guid accountId, AccountMemberRole minimumRole, bool includeArchived, string notFoundMessage, CancellationToken cancellationToken)
    {
        var account = await FindAccessibleAccountAsync(userId, accountId, minimumRole, includeArchived, cancellationToken);
        return account ?? throw new ValidationException(notFoundMessage);
    }
}
