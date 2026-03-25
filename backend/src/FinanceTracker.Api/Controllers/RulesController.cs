using FluentValidation;
using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.Rules.DTOs;
using FinanceTracker.Application.Rules.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("WorkspaceUi")]
[Route("api/rules")]
public sealed class RulesController(
    IRuleService ruleService,
    ICurrentUserService currentUserService,
    IValidator<UpsertTransactionRuleRequest> upsertValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var rules = await ruleService.ListAsync(GetUserId(), cancellationToken);
        return Ok(rules);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertTransactionRuleRequest request, CancellationToken cancellationToken)
    {
        var validation = await upsertValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var rule = await ruleService.CreateAsync(GetUserId(), request, cancellationToken);
        return Created($"/api/rules/{rule.Id}", rule);
    }

    [HttpPut("{ruleId:guid}")]
    public async Task<IActionResult> Update(Guid ruleId, [FromBody] UpsertTransactionRuleRequest request, CancellationToken cancellationToken)
    {
        var validation = await upsertValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var rule = await ruleService.UpdateAsync(GetUserId(), ruleId, request, cancellationToken);
        return Ok(rule);
    }

    [HttpDelete("{ruleId:guid}")]
    public async Task<IActionResult> Delete(Guid ruleId, CancellationToken cancellationToken)
    {
        await ruleService.DeleteAsync(GetUserId(), ruleId, cancellationToken);
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



