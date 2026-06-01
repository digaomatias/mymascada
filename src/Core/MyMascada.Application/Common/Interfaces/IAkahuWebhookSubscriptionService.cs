namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Application service that owns the per-user Akahu webhook subscription lifecycle.
/// Wraps the raw <see cref="IAkahuApiClient"/> subscribe/unsubscribe/list endpoints and
/// keeps local <c>AkahuWebhookSubscription</c> rows in sync.
/// </summary>
public interface IAkahuWebhookSubscriptionService
{
    /// <summary>
    /// Ensures that for the given user there is exactly one healthy subscription per required
    /// webhook type. Idempotent: skips healthy ones, re-subscribes if the local row is stale,
    /// adopts an Akahu-side subscription without re-POSTing when appropriate. Per-type failures
    /// are swallowed so a partial failure never propagates out of the OAuth/save handlers.
    /// </summary>
    Task<EnsureSubscriptionsResult> EnsureSubscriptionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Best-effort teardown: deletes every persisted subscription for the user, calling
    /// DELETE /webhooks/{id} for each. Tolerates Akahu errors (the underlying token revoke
    /// kills the subscription anyway).
    /// </summary>
    Task TearDownSubscriptionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Reconciles drift between local rows and Akahu's authoritative list. Re-subscribes
    /// missing types and adopts orphan Akahu subscriptions when appropriate.
    /// </summary>
    Task<ReconcileResult> ReconcileAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Outcome of <see cref="IAkahuWebhookSubscriptionService.EnsureSubscriptionsAsync"/>.
/// </summary>
public record EnsureSubscriptionsResult(
    IReadOnlyList<string> SubscribedTypes,
    IReadOnlyList<string> AdoptedTypes,
    IReadOnlyList<string> AlreadyHealthyTypes,
    IReadOnlyDictionary<string, string> FailedTypes);

/// <summary>
/// Outcome of <see cref="IAkahuWebhookSubscriptionService.ReconcileAsync"/>.
/// </summary>
public record ReconcileResult(
    IReadOnlyList<string> SubscribedTypes,
    IReadOnlyList<string> AdoptedTypes,
    IReadOnlyList<string> AlreadyHealthyTypes,
    IReadOnlyDictionary<string, string> FailedTypes);
