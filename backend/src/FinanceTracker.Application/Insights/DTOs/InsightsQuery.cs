using FinanceTracker.Application.Reports.DTOs;

namespace FinanceTracker.Application.Insights.DTOs;

public sealed record InsightsQuery(
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    Guid[]? AccountIds = null,
    Guid? AccountId = null,
    Guid[]? CategoryIds = null,
    Guid? CategoryId = null,
    ReportTimeBucket Bucket = ReportTimeBucket.Auto);
