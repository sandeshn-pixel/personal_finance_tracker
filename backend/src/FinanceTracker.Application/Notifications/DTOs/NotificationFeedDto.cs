namespace FinanceTracker.Application.Notifications.DTOs;

public sealed record NotificationFeedDto(
    int UnreadCount,
    IReadOnlyCollection<NotificationDto> Items);