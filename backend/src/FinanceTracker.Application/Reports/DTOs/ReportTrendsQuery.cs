namespace FinanceTracker.Application.Reports.DTOs;

public sealed record ReportTrendsQuery(
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    ReportTimeBucket Bucket = ReportTimeBucket.Auto,
    Guid[]? AccountIds = null,
    Guid? AccountId = null,
    Guid[]? CategoryIds = null,
    Guid? CategoryId = null);
