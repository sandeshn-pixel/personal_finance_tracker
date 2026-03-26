namespace FinanceTracker.Application.Insights.DTOs;

public enum InsightLevel
{
    Info = 1,
    Positive = 2,
    Attention = 3
}

public sealed record InsightItemDto(
    string Key,
    string Title,
    string Message,
    string Basis,
    InsightLevel Level,
    bool IsFallback);

public sealed record InsightsResponseDto(
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    DateTime ComparisonStartUtc,
    DateTime ComparisonEndUtc,
    bool HasSparseData,
    string Summary,
    IReadOnlyCollection<InsightItemDto> Items);
