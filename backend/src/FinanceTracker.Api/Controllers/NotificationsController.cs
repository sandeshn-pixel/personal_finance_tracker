using FinanceTracker.Application.Auth.Interfaces;
using FinanceTracker.Application.Notifications.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(
    INotificationService notificationService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool unreadOnly = false, [FromQuery] int take = 12, CancellationToken cancellationToken = default)
    {
        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        var feed = await notificationService.ListAsync(userId, unreadOnly, take, cancellationToken);
        return Ok(feed);
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        await notificationService.MarkReadAsync(userId, notificationId, cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId ?? throw new InvalidOperationException("Authenticated user is required.");
        await notificationService.MarkAllReadAsync(userId, cancellationToken);
        return NoContent();
    }
}