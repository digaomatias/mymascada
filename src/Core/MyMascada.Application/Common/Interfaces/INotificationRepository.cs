using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Common.Interfaces;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<(List<Notification> Items, int TotalCount)> GetPagedAsync(
        Guid userId,
        int page,
        int pageSize,
        NotificationType? type = null,
        bool? isRead = null,
        CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Notification> CreateAsync(Notification notification, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteExpiredAsync(int retentionDays = 90, CancellationToken cancellationToken = default);
    Task<bool> ExistsByGroupKeyAsync(Guid userId, string groupKey, CancellationToken cancellationToken = default);
    Task<int> CountRecentByTypeAsync(Guid userId, NotificationType type, TimeSpan window, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically checks the per-type rate limit and, if not exceeded, inserts the notification.
    /// Returns <c>null</c> if the rate limit was exceeded (notification was not created).
    /// </summary>
    Task<Notification?> CreateIfRateLimitNotExceededAsync(
        Notification notification,
        NotificationType type,
        TimeSpan rateLimitWindow,
        int maxCount,
        CancellationToken cancellationToken = default);
}
