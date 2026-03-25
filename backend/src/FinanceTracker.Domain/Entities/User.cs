using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.Entities;

public sealed class User : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime? LastLoginUtc { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
    public ICollection<AccountMembership> AccountMemberships { get; set; } = new List<AccountMembership>();
    public ICollection<AccountMembership> InvitedAccountMemberships { get; set; } = new List<AccountMembership>();
    public ICollection<AccountMembership> ModifiedAccountMemberships { get; set; } = new List<AccountMembership>();
    public ICollection<AccountInvite> SentAccountInvites { get; set; } = new List<AccountInvite>();
    public ICollection<AccountInvite> AcceptedAccountInvites { get; set; } = new List<AccountInvite>();
    public ICollection<AccountInvite> RevokedAccountInvites { get; set; } = new List<AccountInvite>();
    public ICollection<Transaction> AuthoredTransactions { get; set; } = new List<Transaction>();
    public ICollection<Transaction> UpdatedTransactions { get; set; } = new List<Transaction>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Budget> Budgets { get; set; } = new List<Budget>();
    public ICollection<Goal> Goals { get; set; } = new List<Goal>();
    public ICollection<GoalEntry> GoalEntries { get; set; } = new List<GoalEntry>();
    public ICollection<RecurringTransactionRule> RecurringTransactionRules { get; set; } = new List<RecurringTransactionRule>();
    public ICollection<UserNotification> Notifications { get; set; } = new List<UserNotification>();
    public ICollection<TransactionRule> TransactionRules { get; set; } = new List<TransactionRule>();
    public UserSettings? Settings { get; set; }
}
