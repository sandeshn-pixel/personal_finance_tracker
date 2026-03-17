using FinanceTracker.Application.Automation.DTOs;
using FinanceTracker.Application.Automation.Interfaces;

namespace FinanceTracker.Infrastructure.Automation;

public sealed class AutomationStatusTracker : IAutomationStatusTracker
{
    private readonly object gate = new();
    private DateTime? lastStartedUtc;
    private DateTime? lastCompletedUtc;
    private bool? lastRunSucceeded;
    private string? lastError;
    private AutomationRunSummaryDto? lastSummary;

    public void RecordStarted(DateTime startedUtc)
    {
        lock (gate)
        {
            lastStartedUtc = startedUtc;
            lastRunSucceeded = null;
            lastError = null;
        }
    }

    public void RecordSucceeded(AutomationRunSummaryDto summary, DateTime completedUtc)
    {
        lock (gate)
        {
            lastCompletedUtc = completedUtc;
            lastRunSucceeded = true;
            lastError = null;
            lastSummary = summary;
        }
    }

    public void RecordFailed(DateTime completedUtc, string errorMessage)
    {
        lock (gate)
        {
            lastCompletedUtc = completedUtc;
            lastRunSucceeded = false;
            lastError = errorMessage;
        }
    }

    public AutomationStatusDto GetSnapshot(bool backgroundProcessingEnabled, int pollingIntervalSeconds)
    {
        lock (gate)
        {
            return new AutomationStatusDto(
                backgroundProcessingEnabled,
                pollingIntervalSeconds,
                lastStartedUtc,
                lastCompletedUtc,
                lastRunSucceeded,
                lastError,
                lastSummary);
        }
    }
}
