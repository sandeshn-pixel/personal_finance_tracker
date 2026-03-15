using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.Budgets.DTOs;
using FinanceTracker.Application.Exports.Interfaces;
using FinanceTracker.Application.Reports.DTOs;
using FinanceTracker.Application.Transactions.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/exports")]
public sealed class ExportsController(
    IExportService exportService,
    ICurrentUserService currentUserService,
    IValidator<TransactionListQuery> transactionQueryValidator,
    IValidator<ReportQuery> reportQueryValidator,
    IValidator<BudgetMonthQuery> budgetMonthQueryValidator) : ControllerBase
{
    [HttpGet("transactions.csv")]
    public async Task<IActionResult> ExportTransactions([FromQuery] TransactionListQuery query, CancellationToken cancellationToken)
    {
        var validation = await transactionQueryValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var export = await exportService.ExportTransactionsCsvAsync(GetUserId(), query, cancellationToken);
        return File(export.Content, export.ContentType, export.FileName);
    }

    [HttpGet("reports/overview.csv")]
    public async Task<IActionResult> ExportReportsOverview([FromQuery] ReportQuery query, CancellationToken cancellationToken)
    {
        var validation = await reportQueryValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var export = await exportService.ExportReportOverviewCsvAsync(GetUserId(), query, cancellationToken);
        return File(export.Content, export.ContentType, export.FileName);
    }

    [HttpGet("budgets/month.csv")]
    public async Task<IActionResult> ExportBudgetMonth([FromQuery] BudgetMonthQuery query, CancellationToken cancellationToken)
    {
        var validation = await budgetMonthQueryValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var export = await exportService.ExportBudgetSummaryCsvAsync(GetUserId(), query, cancellationToken);
        return File(export.Content, export.ContentType, export.FileName);
    }

    private Guid GetUserId() => currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");

    private IActionResult BuildValidationProblem(FluentValidation.Results.ValidationResult validationResult)
    {
        foreach (var error in validationResult.Errors)
        {
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return ValidationProblem(ModelState);
    }
}