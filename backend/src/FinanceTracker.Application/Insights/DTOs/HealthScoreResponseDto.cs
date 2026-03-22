namespace FinanceTracker.Application.Insights.DTOs;

public sealed record HealthScoreResponseDto(
    int Score,
    HealthScoreBand Band,
    bool HasSparseData,
    DateTime LookbackStartUtc,
    DateTime LookbackEndUtc,
    string Summary,
    IReadOnlyCollection<HealthScoreFactorDto> Factors,
    IReadOnlyCollection<string> Suggestions);
