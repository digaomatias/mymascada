using MyMascada.Domain.Enums;

namespace MyMascada.Application.Common.Interfaces;

public interface INotificationService
{
    /// <summary>
    /// Creates a notification and delivers it to enabled channels.
    /// Checks user preferences, rate limits, and idempotency before creating.
    /// </summary>
    /// <param name="periodDeduplicated">
    /// Marks a notification that is deduplicated over an extended period by its
    /// groupKey (e.g. budget alerts: one per category per budget period). Two
    /// effects: (1) skips the per-type daily count cap, which would otherwise
    /// silently drop legitimate distinct alerts (one per category) past the 10th;
    /// (2) treats a soft-deleted prior notification with the same groupKey as
    /// still-existing, so deleting the alert from the bell doesn't cause it to be
    /// recreated on the next run within the same period.
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
        bool periodDeduplicated = false,
        CancellationToken cancellationToken = default);
}
