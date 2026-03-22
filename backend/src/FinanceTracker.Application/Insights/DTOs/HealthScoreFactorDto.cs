namespace FinanceTracker.Application.Insights.DTOs;

public sealed record HealthScoreFactorDto(
    string Key,
    string Title,
    int Score,
    int WeightPercent,
    decimal WeightedPoints,
    decimal MetricValue,
    string MetricLabel,
    string Explanation,
    bool IsFallback);
