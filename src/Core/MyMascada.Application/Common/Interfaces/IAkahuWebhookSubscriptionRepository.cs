using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Repository interface for managing Akahu webhook subscription records.
/// One row per (UserId, WebhookType); soft-delete semantics match
/// <see cref="IAkahuUserCredentialRepository"/>.
/// </summary>
public interface IAkahuWebhookSubscriptionRepository
{
    /// <summary>
    /// Gets all active subscriptions for a user.
    /// </summary>
    Task<IReadOnlyList<AkahuWebhookSubscription>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Looks up a single subscription by Akahu webhook ID.
    /// </summary>
    Task<AkahuWebhookSubscription?> GetByWebhookIdAsync(string webhookId, CancellationToken ct = default);

    /// <summary>
    /// Inserts a new subscription row.
    /// </summary>
    Task<AkahuWebhookSubscription> AddAsync(AkahuWebhookSubscription subscription, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing subscription row (e.g. reconciliation timestamps).
    /// </summary>
    Task UpdateAsync(AkahuWebhookSubscription subscription, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a subscription by primary key.
    /// </summary>
    Task DeleteByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes every subscription belonging to a user.
    /// </summary>
    Task DeleteByUserIdAsync(Guid userId, CancellationToken ct = default);
}
