namespace MyMascada.Application.BackgroundJobs;

/// <summary>
/// Service for reconciling Akahu webhook subscriptions across all active users.
/// Runs periodically to heal drift between MyMascada's stored subscription rows and
/// Akahu's authoritative subscription list.
/// </summary>
public interface IAkahuWebhookSubscriptionReconciliationJobService
{
    /// <summary>
    /// Reconciles webhook subscriptions for every active Akahu user credential.
    /// Called by Hangfire on a recurring schedule.
    /// </summary>
    Task ReconcileAllAsync();
}
