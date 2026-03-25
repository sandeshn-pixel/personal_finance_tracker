using System.Linq.Expressions;
using FinanceTracker.Application.Accounts.DTOs;
using FinanceTracker.Application.Accounts.Interfaces;
using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Financial;

public sealed class AccountService(
    ApplicationDbContext dbContext,
    AccountAccessService accountAccessService) : IAccountService
{
    public async Task<IReadOnlyCollection<AccountDto>> ListAsync(Guid userId, bool includeArchived, CancellationToken cancellationToken)
    {
        return await accountAccessService.QueryAccessibleAccounts(userId, AccountMemberRole.Viewer, includeArchived)
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(Map(userId))
            .ToListAsync(cancellationToken);
    }

    public async Task<AccountDto?> GetAsync(Guid userId, Guid accountId, CancellationToken cancellationToken)
    {
        return await accountAccessService.QueryAccessibleAccounts(userId, AccountMemberRole.Viewer, includeArchived: true)
            .AsNoTracking()
            .Where(x => x.Id == accountId)
            .Select(Map(userId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AccountDto> CreateAsync(Guid userId, CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var openingBalance = decimal.Round(request.OpeningBalance, 2, MidpointRounding.AwayFromZero);
        var account = new Account
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Type = request.Type,
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            OpeningBalance = openingBalance,
            CurrentBalance = openingBalance,
            InstitutionName = request.InstitutionName?.Trim(),
            Last4Digits = request.Last4Digits?.Trim()
        };

        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);

        var ownerDisplayName = await dbContext.Users
            .Where(x => x.Id == userId)
            .Select(x => (x.FirstName + " " + x.LastName).Trim())
            .SingleAsync(cancellationToken);

        return ToDto(account, ownerDisplayName);
    }

    public async Task<AccountDto> UpdateAsync(Guid userId, Guid accountId, UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await accountAccessService.RequireOwnedAccountAsync(userId, accountId, includeArchived: true, cancellationToken);

        var normalizedCurrency = request.CurrencyCode.Trim().ToUpperInvariant();
        if (!string.Equals(account.CurrencyCode, normalizedCurrency, StringComparison.Ordinal))
        {
            var hasTransactions = await dbContext.Transactions.AnyAsync(x => !x.IsDeleted && (x.AccountId == accountId || x.TransferAccountId == accountId), cancellationToken);
            if (hasTransactions)
            {
                throw new ConflictException("Account currency cannot be changed after transactions exist.");
            }
        }

        account.Name = request.Name.Trim();
        account.Type = request.Type;
        account.CurrencyCode = normalizedCurrency;
        account.InstitutionName = request.InstitutionName?.Trim();
        account.Last4Digits = request.Last4Digits?.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);
        var ownerDisplayName = await dbContext.Users
            .Where(x => x.Id == account.UserId)
            .Select(x => (x.FirstName + " " + x.LastName).Trim())
            .SingleAsync(cancellationToken);
        return ToDto(account, ownerDisplayName);
    }

    public async Task ArchiveAsync(Guid userId, Guid accountId, CancellationToken cancellationToken)
    {
        var account = await accountAccessService.RequireOwnedAccountAsync(userId, accountId, includeArchived: true, cancellationToken);
        account.IsArchived = true;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Expression<Func<Account, AccountDto>> Map(Guid userId) => x => new AccountDto(
        x.Id,
        x.Name,
        x.Type,
        x.CurrencyCode,
        x.OpeningBalance,
        x.CurrentBalance,
        x.InstitutionName,
        x.Last4Digits,
        x.IsArchived,
        x.Memberships.Any(),
        x.UserId == userId ? AccountMemberRole.Owner : x.Memberships.Where(m => m.UserId == userId).Select(m => m.Role).FirstOrDefault(),
        (x.User.FirstName + " " + x.User.LastName).Trim(),
        1 + x.Memberships.Count(),
        x.UserId == userId ? x.Invites.Count(i => i.Status == AccountInviteStatus.Pending) : 0);

    private static AccountDto ToDto(Account account, string ownerDisplayName) => new(
        account.Id,
        account.Name,
        account.Type,
        account.CurrencyCode,
        account.OpeningBalance,
        account.CurrentBalance,
        account.InstitutionName,
        account.Last4Digits,
        account.IsArchived,
        false,
        AccountMemberRole.Owner,
        ownerDisplayName,
        1,
        0);
}
