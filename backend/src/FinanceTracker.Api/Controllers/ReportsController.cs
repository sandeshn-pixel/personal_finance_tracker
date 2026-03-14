using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.Reports.DTOs;
using FinanceTracker.Application.Reports.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController(
    IReportService reportService,
    ICurrentUserService currentUserService,
    IValidator<ReportQuery> reportQueryValidator) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<IActionResult> Overview([FromQuery] ReportQuery query, CancellationToken cancellationToken)
    {
        var validation = await reportQueryValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }

            return ValidationProblem(ModelState);
        }

        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var overview = await reportService.GetOverviewAsync(userId, query, cancellationToken);
        return Ok(overview);
    }
}
