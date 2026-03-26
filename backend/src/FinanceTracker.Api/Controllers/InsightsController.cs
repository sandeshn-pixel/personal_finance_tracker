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
    IInsightsService insightsService,
    ICurrentUserService currentUserService,
    IValidator<HealthScoreQuery> healthScoreQueryValidator,
    IValidator<InsightsQuery> insightsQueryValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] InsightsQuery query, CancellationToken cancellationToken)
    {
        var validationProblem = await ValidateAsync(insightsQueryValidator, query, cancellationToken);
        if (validationProblem is not null)
        {
            return validationProblem;
        }

        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var response = await insightsService.GetAsync(userId, query, cancellationToken);
        return Ok(response);
    }

    [HttpGet("health-score")]
    public async Task<IActionResult> HealthScore([FromQuery] HealthScoreQuery query, CancellationToken cancellationToken)
    {
        var validationProblem = await ValidateAsync(healthScoreQueryValidator, query, cancellationToken);
        if (validationProblem is not null)
        {
            return validationProblem;
        }

        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var score = await healthScoreService.GetAsync(userId, query, cancellationToken);
        return Ok(score);
    }

    private async Task<IActionResult?> ValidateAsync<T>(IValidator<T> validator, T query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (validation.IsValid)
        {
            return null;
        }

        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return ValidationProblem(ModelState);
    }
}
