using System.Linq.Expressions;
using FinanceTracker.Application.Accounts.DTOs;
using FinanceTracker.Application.Accounts.Interfaces;
using FinanceTracker.Application.Common;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Financial;

public sealed class AccountService(ApplicationDbContext dbContext) : IAccountService
{
    public async Task<IReadOnlyCollection<AccountDto>> ListAsync(Guid userId, bool includeArchived, CancellationToken cancellationToken)
    {
        return await dbContext.Accounts
            .AsNoTracking()
            .Where(x => x.UserId == userId && (includeArchived || !x.IsArchived))
            .OrderBy(x => x.Name)
            .Select(Map())
            .ToListAsync(cancellationToken);
    }

    public async Task<AccountDto?> GetAsync(Guid userId, Guid accountId, CancellationToken cancellationToken)
    {
        return await dbContext.Accounts
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Id == accountId)
            .Select(Map())
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
        return ToDto(account);
    }

    public async Task<AccountDto> UpdateAsync(Guid userId, Guid accountId, UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == accountId, cancellationToken)
            ?? throw new NotFoundException("Account was not found.");

        var normalizedCurrency = request.CurrencyCode.Trim().ToUpperInvariant();
        if (!string.Equals(account.CurrencyCode, normalizedCurrency, StringComparison.Ordinal))
        {
            var hasTransactions = await dbContext.Transactions.AnyAsync(x => x.UserId == userId && !x.IsDeleted && (x.AccountId == accountId || x.TransferAccountId == accountId), cancellationToken);
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
        return ToDto(account);
    }

    public async Task ArchiveAsync(Guid userId, Guid accountId, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.SingleOrDefaultAsync(x => x.UserId == userId && x.Id == accountId, cancellationToken)
            ?? throw new NotFoundException("Account was not found.");

        account.IsArchived = true;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Expression<Func<Account, AccountDto>> Map() => x => new AccountDto(x.Id, x.Name, x.Type, x.CurrencyCode, x.OpeningBalance, x.CurrentBalance, x.InstitutionName, x.Last4Digits, x.IsArchived);
    private static AccountDto ToDto(Account x) => new(x.Id, x.Name, x.Type, x.CurrencyCode, x.OpeningBalance, x.CurrentBalance, x.InstitutionName, x.Last4Digits, x.IsArchived);
}
