using FinanceTracker.Application.Automation.DTOs;

namespace FinanceTracker.Application.Automation.Interfaces;

public interface IAutomationStatusTracker
{
    void RecordStarted(DateTime startedUtc);
    void RecordSucceeded(AutomationRunSummaryDto summary, DateTime completedUtc);
    void RecordFailed(DateTime completedUtc, string errorMessage);
    AutomationStatusDto GetSnapshot(bool backgroundProcessingEnabled, int pollingIntervalSeconds);
}
