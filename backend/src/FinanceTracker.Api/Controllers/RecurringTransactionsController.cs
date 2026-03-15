using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.RecurringTransactions.DTOs;
using FinanceTracker.Application.RecurringTransactions.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/recurring-transactions")]
public sealed class RecurringTransactionsController(
    IRecurringTransactionService recurringTransactionService,
    ICurrentUserService currentUserService,
    IValidator<CreateRecurringTransactionRequest> createValidator,
    IValidator<UpdateRecurringTransactionRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var rules = await recurringTransactionService.ListAsync(userId, cancellationToken);
        return Ok(rules);
    }

    [HttpGet("{ruleId:guid}")]
    public async Task<IActionResult> Get(Guid ruleId, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var rule = await recurringTransactionService.GetAsync(userId, ruleId, cancellationToken);
        return rule is null ? NotFound() : Ok(rule);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRecurringTransactionRequest request, CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var created = await recurringTransactionService.CreateAsync(userId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{ruleId:guid}")]
    public async Task<IActionResult> Update(Guid ruleId, [FromBody] UpdateRecurringTransactionRequest request, CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var updated = await recurringTransactionService.UpdateAsync(userId, ruleId, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{ruleId:guid}/pause")]
    public async Task<IActionResult> Pause(Guid ruleId, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var updated = await recurringTransactionService.PauseAsync(userId, ruleId, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{ruleId:guid}/resume")]
    public async Task<IActionResult> Resume(Guid ruleId, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var updated = await recurringTransactionService.ResumeAsync(userId, ruleId, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{ruleId:guid}")]
    public async Task<IActionResult> Delete(Guid ruleId, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        await recurringTransactionService.DeleteAsync(userId, ruleId, cancellationToken);
        return NoContent();
    }

    [HttpPost("process-due")]
    public async Task<IActionResult> ProcessDue(CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var summary = await recurringTransactionService.ProcessDueAsync(userId, DateTime.UtcNow, cancellationToken);
        return Ok(summary);
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