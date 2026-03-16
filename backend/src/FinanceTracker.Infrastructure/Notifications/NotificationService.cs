using FinanceTracker.Application.Notifications.DTOs;
using FinanceTracker.Application.Notifications.Interfaces;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Notifications;

public sealed class NotificationService(ApplicationDbContext dbContext) : INotificationService
{
    public async Task<NotificationFeedDto> ListAsync(Guid userId, bool unreadOnly, int take, CancellationToken cancellationToken)
    {
        var boundedTake = Math.Clamp(take, 1, 25);
        var baseQuery = dbContext.UserNotifications
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (unreadOnly)
        {
            baseQuery = baseQuery.Where(x => x.ReadAtUtc == null);
        }

        var unreadCount = await dbContext.UserNotifications
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.ReadAtUtc == null)
            .CountAsync(cancellationToken);

        var items = await baseQuery
            .OrderByDescending(x => x.CreatedUtc)
            .Take(boundedTake)
            .Select(x => new NotificationDto(
                x.Id,
                x.Type,
                x.Level,
                x.Title,
                x.Message,
                x.Route,
                x.ReadAtUtc != null,
                x.CreatedUtc,
                x.ReadAtUtc))
            .ToListAsync(cancellationToken);

        return new NotificationFeedDto(unreadCount, items);
    }

    public async Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = await dbContext.UserNotifications
            .SingleOrDefaultAsync(x => x.UserId == userId && x.Id == notificationId, cancellationToken);

        if (notification is null || notification.ReadAtUtc.HasValue)
        {
            return;
        }

        notification.ReadAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var notifications = await dbContext.UserNotifications
            .Where(x => x.UserId == userId && x.ReadAtUtc == null)
            .ToListAsync(cancellationToken);

        if (notifications.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var notification in notifications)
        {
            notification.ReadAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> PublishAsync(PublishNotificationRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.DeduplicationKey))
        {
            var exists = await dbContext.UserNotifications
                .AsNoTracking()
                .AnyAsync(x => x.UserId == request.UserId && x.DeduplicationKey == request.DeduplicationKey, cancellationToken);

            if (exists)
            {
                return false;
            }
        }

        dbContext.UserNotifications.Add(new UserNotification
        {
            UserId = request.UserId,
            Type = request.Type,
            Level = request.Level,
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            Route = string.IsNullOrWhiteSpace(request.Route) ? null : request.Route.Trim(),
            DeduplicationKey = string.IsNullOrWhiteSpace(request.DeduplicationKey) ? null : request.DeduplicationKey.Trim(),
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}