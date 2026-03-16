using FinanceTracker.Application.Notifications.DTOs;

namespace FinanceTracker.Application.Notifications.Interfaces;

public interface INotificationService
{
    Task<NotificationFeedDto> ListAsync(Guid userId, bool unreadOnly, int take, CancellationToken cancellationToken);
    Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken);
    Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> PublishAsync(PublishNotificationRequest request, CancellationToken cancellationToken);
}