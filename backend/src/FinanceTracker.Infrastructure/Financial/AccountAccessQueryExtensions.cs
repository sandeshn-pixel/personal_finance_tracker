using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Infrastructure.Financial;

internal static class AccountAccessQueryExtensions
{
    public static IQueryable<Account> WhereUserHasMinimumRole(this IQueryable<Account> accounts, Guid userId, AccountMemberRole minimumRole)
    {
        var minimum = (int)minimumRole;
        return accounts.Where(x => x.UserId == userId || x.Memberships.Any(m => m.UserId == userId && (int)m.Role >= minimum));
    }

    public static IQueryable<Transaction> WhereUserCanView(this IQueryable<Transaction> transactions, Guid userId)
    {
        return transactions.Where(x => !x.IsDeleted && (
            x.Account.UserId == userId
            || x.Account.Memberships.Any(m => m.UserId == userId)
            || (x.TransferAccount != null && (x.TransferAccount.UserId == userId || x.TransferAccount.Memberships.Any(m => m.UserId == userId)))));
    }
}
