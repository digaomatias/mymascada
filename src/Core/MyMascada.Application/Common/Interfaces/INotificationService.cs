using MyMascada.Domain.Enums;

namespace MyMascada.Application.Common.Interfaces;

public interface INotificationService
{
    /// <summary>
    /// Creates a notification and delivers it to enabled channels.
    /// Checks user preferences, rate limits, and idempotency before creating.
    /// </summary>
    Task CreateNotificationAsync(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        string? data = null,
        NotificationPriority priority = NotificationPriority.Normal,
        string? groupKey = null,
        DateTime? expiresAt = null,
        CancellationToken cancellationToken = default);
}
