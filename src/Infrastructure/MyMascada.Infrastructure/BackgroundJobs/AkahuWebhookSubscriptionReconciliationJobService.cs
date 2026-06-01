using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyMascada.Application.BackgroundJobs;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire-based background job that reconciles Akahu webhook subscriptions for every
/// active user. Runs daily to heal drift between MyMascada's stored subscription rows and
/// Akahu's authoritative subscription list (the safety net for partial subscription failures).
/// </summary>
public class AkahuWebhookSubscriptionReconciliationJobService
    : IAkahuWebhookSubscriptionReconciliationJobService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<AkahuWebhookSubscriptionReconciliationJobService> _logger;

    public AkahuWebhookSubscriptionReconciliationJobService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<AkahuWebhookSubscriptionReconciliationJobService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Reconciles webhook subscriptions for every active Akahu user credential.
    /// Scheduled to run daily at 4:00 AM.
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public async Task ReconcileAllAsync()
    {
        _logger.LogInformation("Starting Akahu webhook subscription reconciliation job");

        try
        {
            List<Guid> userIds;
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var credentialRepository = scope.ServiceProvider.GetRequiredService<IAkahuUserCredentialRepository>();
                var credentials = await credentialRepository.GetActiveCredentialsAsync();
                userIds = credentials.Select(c => c.UserId).Distinct().ToList();
            }

            if (userIds.Count == 0)
            {
                _logger.LogDebug("No active Akahu credentials to reconcile webhook subscriptions for");
                return;
            }

            _logger.LogInformation("Reconciling Akahu webhook subscriptions for {Count} users", userIds.Count);

            var successCount = 0;
            var failCount = 0;

            foreach (var userId in userIds)
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var subscriptionService = scope.ServiceProvider
                        .GetRequiredService<IAkahuWebhookSubscriptionService>();

                    await subscriptionService.ReconcileAsync(userId);
                    successCount++;
                }
                catch (Exception ex)
                {
                    failCount++;
                    _logger.LogError(ex,
                        "Failed to reconcile Akahu webhook subscriptions for user {UserId}. Continuing with remaining users.",
                        userId);
                }
            }

            _logger.LogInformation(
                "Akahu webhook subscription reconciliation job completed: {Success} succeeded, {Failed} failed",
                successCount, failCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Akahu webhook subscription reconciliation job failed unexpectedly");
            throw; // Let Hangfire retry
        }
    }
}
