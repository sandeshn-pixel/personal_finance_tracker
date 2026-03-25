using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.Insights.DTOs;
using FinanceTracker.Application.Insights.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("InsightsRead")]
[Route("api/insights")]
public sealed class InsightsController(
    IHealthScoreService healthScoreService,
    ICurrentUserService currentUserService,
    IValidator<HealthScoreQuery> healthScoreQueryValidator) : ControllerBase
{
    [HttpGet("health-score")]
    public async Task<IActionResult> HealthScore([FromQuery] HealthScoreQuery query, CancellationToken cancellationToken)
    {
        var validation = await healthScoreQueryValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }

        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var score = await healthScoreService.GetAsync(userId, query, cancellationToken);
        return Ok(score);
    }
}
