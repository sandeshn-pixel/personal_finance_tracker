using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Notifications.DTOs;

public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    NotificationLevel Level,
    string Title,
    string Message,
    string? Route,
    bool IsRead,
    DateTime CreatedUtc,
    DateTime? ReadAtUtc);