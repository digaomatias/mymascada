using MyMascada.Domain.Enums;

namespace MyMascada.Application.Common.Interfaces;

public interface INotificationService
{
    /// <summary>
    /// Creates a notification and delivers it to enabled channels.
    /// Checks user preferences, rate limits, and idempotency before creating.
    /// </summary>
    /// <param name="bypassDailyLimit">
    /// Skip the per-type daily count cap. Set for groupKey-deduplicated fan-out
    /// producers (e.g. budget alerts, one per category per period) where the cap
    /// would silently drop legitimate distinct alerts past the 10th. The groupKey
    /// unique constraint still prevents duplicates.
    /// </param>
    Task CreateNotificationAsync(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        string? data = null,
        NotificationPriority priority = NotificationPriority.Normal,
        string? groupKey = null,
        DateTime? expiresAt = null,
        bool bypassDailyLimit = false,
        CancellationToken cancellationToken = default);
}
