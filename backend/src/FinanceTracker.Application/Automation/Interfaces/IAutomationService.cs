using FinanceTracker.Application.Automation.DTOs;

namespace FinanceTracker.Application.Automation.Interfaces;

public interface IAutomationService
{
    Task<AutomationRunSummaryDto> RunAsync(DateTime asOfUtc, CancellationToken cancellationToken);
}