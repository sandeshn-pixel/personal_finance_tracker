using FluentValidation;
using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.Transactions.DTOs;
using FinanceTracker.Application.Transactions.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
public sealed class TransactionsController(
    ITransactionService transactionService,
    ICurrentUserService currentUserService,
    IValidator<UpsertTransactionRequest> upsertValidator,
    IValidator<TransactionListQuery> queryValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] TransactionListQuery query, CancellationToken cancellationToken)
    {
        var validation = await queryValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var items = await transactionService.ListAsync(GetUserId(), query, cancellationToken);
        return Ok(items);
    }

    [HttpGet("{transactionId:guid}")]
    public async Task<IActionResult> Get(Guid transactionId, CancellationToken cancellationToken)
    {
        var transaction = await transactionService.GetAsync(GetUserId(), transactionId, cancellationToken);
        return transaction is null ? NotFound() : Ok(transaction);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertTransactionRequest request, CancellationToken cancellationToken)
    {
        var validation = await upsertValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var transaction = await transactionService.CreateAsync(GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { transactionId = transaction.Id }, transaction);
    }

    [HttpPut("{transactionId:guid}")]
    public async Task<IActionResult> Update(Guid transactionId, [FromBody] UpsertTransactionRequest request, CancellationToken cancellationToken)
    {
        var validation = await upsertValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var transaction = await transactionService.UpdateAsync(GetUserId(), transactionId, request, cancellationToken);
        return Ok(transaction);
    }

    [HttpDelete("{transactionId:guid}")]
    public async Task<IActionResult> Delete(Guid transactionId, CancellationToken cancellationToken)
    {
        await transactionService.DeleteAsync(GetUserId(), transactionId, cancellationToken);
        return NoContent();
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
