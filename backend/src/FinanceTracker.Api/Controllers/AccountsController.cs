using FluentValidation;
using FinanceTracker.Application.Accounts.DTOs;
using FinanceTracker.Application.Accounts.Interfaces;
using FinanceTracker.Application.Auth.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/accounts")]
public sealed class AccountsController(
    IAccountService accountService,
    ICurrentUserService currentUserService,
    IValidator<CreateAccountRequest> createValidator,
    IValidator<UpdateAccountRequest> updateValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeArchived, CancellationToken cancellationToken)
    {
        var items = await accountService.ListAsync(GetUserId(), includeArchived, cancellationToken);
        return Ok(items);
    }

    [HttpGet("{accountId:guid}")]
    public async Task<IActionResult> Get(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await accountService.GetAsync(GetUserId(), accountId, cancellationToken);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var account = await accountService.CreateAsync(GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { accountId = account.Id }, account);
    }

    [HttpPut("{accountId:guid}")]
    public async Task<IActionResult> Update(Guid accountId, [FromBody] UpdateAccountRequest request, CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var account = await accountService.UpdateAsync(GetUserId(), accountId, request, cancellationToken);
        return Ok(account);
    }

    [HttpDelete("{accountId:guid}")]
    public async Task<IActionResult> Archive(Guid accountId, CancellationToken cancellationToken)
    {
        await accountServiceArchiveAsync(accountId, cancellationToken);
        return NoContent();
    }

    private Task accountServiceArchiveAsync(Guid accountId, CancellationToken cancellationToken)
        => accountService.ArchiveAsync(GetUserId(), accountId, cancellationToken);

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
