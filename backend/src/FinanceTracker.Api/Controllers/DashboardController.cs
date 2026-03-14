using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.Dashboard.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(IDashboardService dashboardService, ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
    {
        var summary = await dashboardService.GetSummaryAsync(currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required."), cancellationToken);
        return Ok(summary);
    }
}
