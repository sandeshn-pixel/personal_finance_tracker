namespace FinanceTracker.Application.Automation.DTOs;

public sealed record AutomationStatusDto(
    bool BackgroundProcessingEnabled,
    int PollingIntervalSeconds,
    DateTime? LastStartedUtc,
    DateTime? LastCompletedUtc,
    bool? LastRunSucceeded,
    string? LastError,
    AutomationRunSummaryDto? LastSummary);
