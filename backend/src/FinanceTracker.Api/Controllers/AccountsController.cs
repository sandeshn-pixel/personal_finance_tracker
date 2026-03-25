using FluentValidation;
using FinanceTracker.Application.Accounts.DTOs;
using FinanceTracker.Application.Accounts.Interfaces;
using FinanceTracker.Application.Auth.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("WorkspaceUi")]
[Route("api/accounts")]
public sealed class AccountsController(
    IAccountService accountService,
    IAccountMembershipService accountMembershipService,
    ICurrentUserService currentUserService,
    IValidator<CreateAccountRequest> createValidator,
    IValidator<UpdateAccountRequest> updateValidator,
    IValidator<InviteAccountMemberRequest> inviteValidator,
    IValidator<AcceptAccountInviteRequest> acceptInviteValidator,
    IValidator<UpdateAccountMemberRequest> updateMemberValidator) : ControllerBase
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
        await accountService.ArchiveAsync(GetUserId(), accountId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{accountId:guid}/members")]
    public async Task<IActionResult> ListMembers(Guid accountId, CancellationToken cancellationToken)
    {
        var members = await accountMembershipService.ListAsync(GetUserId(), accountId, cancellationToken);
        return Ok(members);
    }

    [HttpGet("{accountId:guid}/invites")]
    public async Task<IActionResult> ListPendingInvites(Guid accountId, CancellationToken cancellationToken)
    {
        var invites = await accountMembershipService.ListPendingInvitesAsync(GetUserId(), accountId, cancellationToken);
        return Ok(invites);
    }

    [HttpPost("{accountId:guid}/invite")]
    public async Task<IActionResult> Invite(Guid accountId, [FromBody] InviteAccountMemberRequest request, CancellationToken cancellationToken)
    {
        var validation = await inviteValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var invite = await accountMembershipService.InviteAsync(GetUserId(), accountId, request, cancellationToken);
        return Ok(invite);
    }

    [HttpPost("{accountId:guid}/invites/{inviteId:guid}/resend")]
    public async Task<IActionResult> ResendInvite(Guid accountId, Guid inviteId, CancellationToken cancellationToken)
    {
        var invite = await accountMembershipService.ResendInviteAsync(GetUserId(), accountId, inviteId, cancellationToken);
        return Ok(invite);
    }

    [AllowAnonymous]
    [HttpGet("invites/preview")]
    public async Task<IActionResult> PreviewInvite([FromQuery] string token, CancellationToken cancellationToken)
    {
        var preview = await accountMembershipService.PreviewInviteAsync(currentUserService.UserId, token, cancellationToken);
        return Ok(preview);
    }

    [HttpPost("invites/accept")]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptAccountInviteRequest request, CancellationToken cancellationToken)
    {
        var validation = await acceptInviteValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var member = await accountMembershipService.AcceptInviteAsync(GetUserId(), request, cancellationToken);
        return Ok(member);
    }

    [HttpPut("{accountId:guid}/members/{memberUserId:guid}")]
    public async Task<IActionResult> UpdateMember(Guid accountId, Guid memberUserId, [FromBody] UpdateAccountMemberRequest request, CancellationToken cancellationToken)
    {
        var validation = await updateMemberValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BuildValidationProblem(validation);
        }

        var member = await accountMembershipService.UpdateAsync(GetUserId(), accountId, memberUserId, request, cancellationToken);
        return Ok(member);
    }

    [HttpDelete("{accountId:guid}/members/{memberUserId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid accountId, Guid memberUserId, CancellationToken cancellationToken)
    {
        await accountMembershipService.RemoveAsync(GetUserId(), accountId, memberUserId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{accountId:guid}/invites/{inviteId:guid}")]
    public async Task<IActionResult> RevokeInvite(Guid accountId, Guid inviteId, CancellationToken cancellationToken)
    {
        await accountMembershipService.RevokeInviteAsync(GetUserId(), accountId, inviteId, cancellationToken);
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

