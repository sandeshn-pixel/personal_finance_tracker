namespace FinanceTracker.Application.Reports.DTOs;

public sealed record ReportQuery(
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    Guid[]? AccountIds,
    Guid? AccountId);
