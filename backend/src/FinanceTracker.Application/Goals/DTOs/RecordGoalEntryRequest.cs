namespace FinanceTracker.Application.Goals.DTOs;

public sealed class RecordGoalEntryRequest
{
    public decimal Amount { get; init; }
    public DateTime? OccurredAtUtc { get; init; }
    public string? Note { get; init; }
}