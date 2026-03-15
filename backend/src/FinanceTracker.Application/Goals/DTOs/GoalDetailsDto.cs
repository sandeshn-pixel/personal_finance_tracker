namespace FinanceTracker.Application.Goals.DTOs;

public sealed record GoalDetailsDto(GoalDto Goal, IReadOnlyCollection<GoalEntryDto> Entries);