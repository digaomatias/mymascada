using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

public interface IUserDeviceRepository
{
    /// <summary>
    /// Idempotently registers a device token for a user. If the token already exists
    /// (for this or another user) the row is updated/reassigned and LastSeenAt refreshed.
    /// </summary>
    Task<UserDevice> UpsertAsync(Guid userId, string fcmToken, string platform, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a device token registration owned by the given user. No-op if not found.
    /// </summary>
    Task DeleteByTokenAsync(Guid userId, string fcmToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all active (non-deleted) devices registered for the user.
    /// </summary>
    Task<List<UserDevice>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes device rows for tokens FCM reported as no longer valid (unregistered).
    /// </summary>
    Task RemoveTokensAsync(IReadOnlyCollection<string> fcmTokens, CancellationToken cancellationToken = default);
}
