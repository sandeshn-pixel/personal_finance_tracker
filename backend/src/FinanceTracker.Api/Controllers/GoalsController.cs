using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.Goals.DTOs;
using FinanceTracker.Application.Goals.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/goals")]
public sealed class GoalsController(
    IGoalService goalService,
    ICurrentUserService currentUserService,
    IValidator<CreateGoalRequest> createValidator,
    IValidator<UpdateGoalRequest> updateValidator,
    IValidator<RecordGoalEntryRequest> entryValidator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var goals = await goalService.ListAsync(userId, cancellationToken);
        return Ok(goals);
    }

    [HttpGet("{goalId:guid}")]
    public async Task<IActionResult> Get(Guid goalId, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var goal = await goalService.GetAsync(userId, goalId, cancellationToken);
        return goal is null ? NotFound() : Ok(goal);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGoalRequest request, CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var created = await goalService.CreateAsync(userId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{goalId:guid}")]
    public async Task<IActionResult> Update(Guid goalId, [FromBody] UpdateGoalRequest request, CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var updated = await goalService.UpdateAsync(userId, goalId, request, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{goalId:guid}/contributions")]
    public async Task<IActionResult> Contribute(Guid goalId, [FromBody] RecordGoalEntryRequest request, CancellationToken cancellationToken)
    {
        var validation = await entryValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var goal = await goalService.RecordContributionAsync(userId, goalId, request, cancellationToken);
        return Ok(goal);
    }

    [HttpPost("{goalId:guid}/withdrawals")]
    public async Task<IActionResult> Withdraw(Guid goalId, [FromBody] RecordGoalEntryRequest request, CancellationToken cancellationToken)
    {
        var validation = await entryValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var goal = await goalService.RecordWithdrawalAsync(userId, goalId, request, cancellationToken);
        return Ok(goal);
    }

    [HttpPost("{goalId:guid}/complete")]
    public async Task<IActionResult> Complete(Guid goalId, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var goal = await goalService.MarkCompletedAsync(userId, goalId, cancellationToken);
        return Ok(goal);
    }

    [HttpPost("{goalId:guid}/archive")]
    public async Task<IActionResult> Archive(Guid goalId, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var goal = await goalService.ArchiveAsync(userId, goalId, cancellationToken);
        return Ok(goal);
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