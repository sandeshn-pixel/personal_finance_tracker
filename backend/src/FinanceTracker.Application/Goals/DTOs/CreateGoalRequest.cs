namespace FinanceTracker.Application.Goals.DTOs;

public sealed class CreateGoalRequest
{
    public string Name { get; init; } = string.Empty;
    public decimal TargetAmount { get; init; }
    public DateTime? TargetDateUtc { get; init; }
    public Guid? LinkedAccountId { get; init; }
    public string? Icon { get; init; }
    public string? Color { get; init; }
}