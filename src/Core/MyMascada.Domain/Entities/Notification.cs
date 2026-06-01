using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents an in-app notification sent to a user.
/// Supports deep linking via the Data JSON payload.
/// </summary>
public class Notification : BaseEntity<Guid>
{
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// JSON payload for deep linking (e.g. { "transactionId": 123, "accountId": 456 })
    /// </summary>
    public string? Data { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Idempotency key to prevent duplicate notifications (e.g. "budget-threshold-80-budgetId-5")
    /// </summary>
    public string? GroupKey { get; set; }

    public void MarkAsRead()
    {
        if (IsRead) return;
        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }

    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
}
