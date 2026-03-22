using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.Insights.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("ReportHeavy")]
[Route("api/insights")]
public sealed class InsightsController(IHealthScoreService healthScoreService, ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet("health-score")]
    public async Task<IActionResult> HealthScore(CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var score = await healthScoreService.GetAsync(userId, cancellationToken);
        return Ok(score);
    }
}
