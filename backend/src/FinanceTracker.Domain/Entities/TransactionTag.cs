namespace FinanceTracker.Domain.Entities;

public sealed class TransactionTag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransactionId { get; set; }
    public string Value { get; set; } = string.Empty;

    public Transaction Transaction { get; set; } = null!;
}
