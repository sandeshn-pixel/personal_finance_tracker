namespace FinanceTracker.Application.Reports.DTOs;

public sealed record NetWorthTrendPointDto(
    DateTime PeriodStartUtc,
    string Label,
    decimal NetWorth,
    decimal AssetBalance,
    decimal LiabilityBalance);

public sealed record NetWorthReportDto(
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    ReportTimeBucket Bucket,
    decimal CurrentNetWorth,
    decimal StartingNetWorth,
    decimal ChangeAmount,
    int IncludedAccountCount,
    int IncludedLiabilityAccountCount,
    string BasisDescription,
    IReadOnlyCollection<NetWorthTrendPointDto> Points);
