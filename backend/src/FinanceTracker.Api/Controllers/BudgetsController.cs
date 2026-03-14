using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.Budgets.DTOs;
using FinanceTracker.Application.Budgets.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/budgets")]
public sealed class BudgetsController(
    IBudgetService budgetService,
    ICurrentUserService currentUserService,
    IValidator<CreateBudgetRequest> createValidator,
    IValidator<UpdateBudgetRequest> updateValidator,
    IValidator<BudgetMonthQuery> monthQueryValidator,
    IValidator<CopyBudgetsRequest> copyValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] BudgetMonthQuery query, CancellationToken cancellationToken)
    {
        var validation = await monthQueryValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var budgets = await budgetService.ListByMonthAsync(userId, query, cancellationToken);
        return Ok(budgets);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] BudgetMonthQuery query, CancellationToken cancellationToken)
    {
        var validation = await monthQueryValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var summary = await budgetService.GetSummaryAsync(userId, query, cancellationToken);
        return Ok(summary);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBudgetRequest request, CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var created = await budgetService.CreateAsync(userId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{budgetId:guid}")]
    public async Task<IActionResult> Update(Guid budgetId, [FromBody] UpdateBudgetRequest request, CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var updated = await budgetService.UpdateAsync(userId, budgetId, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{budgetId:guid}")]
    public async Task<IActionResult> Delete(Guid budgetId, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        await budgetService.DeleteAsync(userId, budgetId, cancellationToken);
        return NoContent();
    }

    [HttpPost("copy-previous-month")]
    public async Task<IActionResult> CopyPreviousMonth([FromBody] CopyBudgetsRequest request, CancellationToken cancellationToken)
    {
        var validation = await copyValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var budgets = await budgetService.CopyPreviousMonthAsync(userId, request, cancellationToken);
        return Ok(budgets);
    }

    private IActionResult BuildValidationProblem(FluentValidation.Results.ValidationResult validationResult)
    {
        foreach (var error in validationResult.Errors)
        {
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return ValidationProblem(ModelState);
    }
}
