using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.Reports.DTOs;
using FinanceTracker.Application.Reports.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("ReportHeavy")]
[Route("api/reports")]
public sealed class ReportsController(
    IReportService reportService,
    ICurrentUserService currentUserService,
    IValidator<ReportQuery> reportQueryValidator,
    IValidator<ReportTrendsQuery> reportTrendsQueryValidator,
    IValidator<ReportNetWorthQuery> reportNetWorthQueryValidator) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<IActionResult> Overview([FromQuery] ReportQuery query, CancellationToken cancellationToken)
    {
        var validationProblem = await ValidateAsync(reportQueryValidator, query, cancellationToken);
        if (validationProblem is not null)
        {
            return validationProblem;
        }

        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var overview = await reportService.GetOverviewAsync(userId, query, cancellationToken);
        return Ok(overview);
    }

    [HttpGet("trends")]
    public async Task<IActionResult> Trends([FromQuery] ReportTrendsQuery query, CancellationToken cancellationToken)
    {
        var validationProblem = await ValidateAsync(reportTrendsQueryValidator, query, cancellationToken);
        if (validationProblem is not null)
        {
            return validationProblem;
        }

        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var trends = await reportService.GetTrendsAsync(userId, query, cancellationToken);
        return Ok(trends);
    }

    [HttpGet("net-worth")]
    public async Task<IActionResult> NetWorth([FromQuery] ReportNetWorthQuery query, CancellationToken cancellationToken)
    {
        var validationProblem = await ValidateAsync(reportNetWorthQueryValidator, query, cancellationToken);
        if (validationProblem is not null)
        {
            return validationProblem;
        }

        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var netWorth = await reportService.GetNetWorthAsync(userId, query, cancellationToken);
        return Ok(netWorth);
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
