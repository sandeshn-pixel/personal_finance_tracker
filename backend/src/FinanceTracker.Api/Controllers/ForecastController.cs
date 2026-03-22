using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.Forecasting.DTOs;
using FinanceTracker.Application.Forecasting.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("ReportHeavy")]
[Route("api/forecast")]
public sealed class ForecastController(
    IForecastService forecastService,
    ICurrentUserService currentUserService,
    IValidator<ForecastQuery> forecastQueryValidator) : ControllerBase
{
    [HttpGet("month")]
    public async Task<IActionResult> Month([FromQuery] ForecastQuery query, CancellationToken cancellationToken)
    {
        var validation = await forecastQueryValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }

        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var summary = await forecastService.GetMonthSummaryAsync(userId, query, cancellationToken);
        return Ok(summary);
    }

    [HttpGet("daily")]
    public async Task<IActionResult> Daily([FromQuery] ForecastQuery query, CancellationToken cancellationToken)
    {
        var validation = await forecastQueryValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }

        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var forecast = await forecastService.GetDailyProjectionAsync(userId, query, cancellationToken);
        return Ok(forecast);
    }
}
