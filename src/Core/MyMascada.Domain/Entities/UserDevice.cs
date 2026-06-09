using MyMascada.Domain.Common;

namespace MyMascada.Domain.Entities;

/// <summary>
/// A mobile device registered for push notifications via Firebase Cloud Messaging.
/// One row per FCM token; a user may have multiple devices.
/// </summary>
public class UserDevice : BaseEntity<Guid>
{
    public Guid UserId { get; set; }

    /// <summary>
    /// Firebase Cloud Messaging registration token. Globally unique —
    /// if a different user logs in on the same device the row is reassigned.
    /// </summary>
    public string FcmToken { get; set; } = string.Empty;

    /// <summary>
    /// Device platform, e.g. "ios" or "android".
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp of the last registration ping from this device.
    /// Used to expire stale device registrations.
    /// </summary>
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}
