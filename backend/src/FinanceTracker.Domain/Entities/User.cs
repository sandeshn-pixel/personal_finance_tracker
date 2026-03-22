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
