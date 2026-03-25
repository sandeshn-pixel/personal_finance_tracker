using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Transactions.DTOs;

public sealed class TransactionListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public DateTime? StartDateUtc { get; init; }
    public DateTime? EndDateUtc { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? AccountId { get; init; }
    public Guid[]? AccountIds { get; init; }
    public TransactionType? Type { get; init; }
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
    public string? Search { get; init; }
}
