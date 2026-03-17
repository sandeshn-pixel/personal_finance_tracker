using FinanceTracker.Application.Automation.Interfaces;
using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Infrastructure.Automation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/automation")]
public sealed class AutomationController(
    IAutomationStatusTracker automationStatusTracker,
    ICurrentUserService currentUserService,
    IOptionsMonitor<AutomationOptions> optionsMonitor) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult Status()
    {
        _ = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var options = optionsMonitor.CurrentValue;
        return Ok(automationStatusTracker.GetSnapshot(options.EnableBackgroundProcessing, options.PollingIntervalSeconds));
    }
}
