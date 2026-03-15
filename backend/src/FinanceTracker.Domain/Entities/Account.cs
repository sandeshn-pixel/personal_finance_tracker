using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Domain.Entities;

public sealed class Account : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public string CurrencyCode { get; set; } = "INR";
    public decimal OpeningBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public string? InstitutionName { get; set; }
    public string? Last4Digits { get; set; }
    public bool IsArchived { get; set; }

    public User User { get; set; } = null!;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Transaction> TransferTransactions { get; set; } = new List<Transaction>();
    public ICollection<Goal> Goals { get; set; } = new List<Goal>();
    public ICollection<GoalEntry> GoalEntries { get; set; } = new List<GoalEntry>();
    public ICollection<RecurringTransactionRule> RecurringTransactionRules { get; set; } = new List<RecurringTransactionRule>();
    public ICollection<RecurringTransactionRule> RecurringTransferTransactionRules { get; set; } = new List<RecurringTransactionRule>();
}
