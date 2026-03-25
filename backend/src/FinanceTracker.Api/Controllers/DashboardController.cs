using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.Dashboard.DTOs;
using FinanceTracker.Application.Dashboard.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(
    IDashboardService dashboardService,
    ICurrentUserService currentUserService,
    IValidator<DashboardQuery> dashboardQueryValidator) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] DashboardQuery query, CancellationToken cancellationToken)
    {
        var validation = await dashboardQueryValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }

        var summary = await dashboardService.GetSummaryAsync(currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required."), query, cancellationToken);
        return Ok(summary);
    }
}
