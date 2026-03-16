using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Notifications.DTOs;

public sealed record PublishNotificationRequest(
    Guid UserId,
    NotificationType Type,
    NotificationLevel Level,
    string Title,
    string Message,
    string? Route,
    string? DeduplicationKey);