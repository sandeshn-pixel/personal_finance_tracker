namespace FinanceTracker.Application.Reports.DTOs;

public sealed record ReportNetWorthQuery(
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    ReportTimeBucket Bucket = ReportTimeBucket.Auto,
    Guid[]? AccountIds = null,
    Guid? AccountId = null);
