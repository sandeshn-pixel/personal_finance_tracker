using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Domain.Entities;

public sealed class Transaction : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? TransferAccountId { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public DateTime DateUtc { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Note { get; set; }
    public string? Merchant { get; set; }
    public string? PaymentMethod { get; set; }
    public Guid? RecurringTransactionId { get; set; }
    public bool IsDeleted { get; set; }

    public User User { get; set; } = null!;
    public Account Account { get; set; } = null!;
    public Account? TransferAccount { get; set; }
    public Category? Category { get; set; }
    public ICollection<TransactionTag> Tags { get; set; } = new List<TransactionTag>();
}
