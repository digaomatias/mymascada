using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Infrastructure.Services.BankIntegration.Providers;

/// <summary>
/// Default implementation of <see cref="IAkahuWebhookSubscriptionService"/>.
/// </summary>
public class AkahuWebhookSubscriptionService : IAkahuWebhookSubscriptionService
{
    private static readonly string[] RequiredTypes =
    {
        AkahuWebhookTypes.Token,
        AkahuWebhookTypes.Account,
        AkahuWebhookTypes.Transaction
    };

    private readonly IAkahuApiClient _akahuApiClient;
    private readonly IAkahuUserCredentialRepository _credentialRepository;
    private readonly IAkahuWebhookSubscriptionRepository _subscriptionRepository;
    private readonly ISettingsEncryptionService _encryptionService;
    private readonly IApplicationLogger<AkahuWebhookSubscriptionService> _logger;

    public AkahuWebhookSubscriptionService(
        IAkahuApiClient akahuApiClient,
        IAkahuUserCredentialRepository credentialRepository,
        IAkahuWebhookSubscriptionRepository subscriptionRepository,
        ISettingsEncryptionService encryptionService,
        IApplicationLogger<AkahuWebhookSubscriptionService> logger)
    {
        _akahuApiClient = akahuApiClient ?? throw new ArgumentNullException(nameof(akahuApiClient));
        _credentialRepository = credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
        _subscriptionRepository = subscriptionRepository ?? throw new ArgumentNullException(nameof(subscriptionRepository));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<EnsureSubscriptionsResult> EnsureSubscriptionsAsync(Guid userId, CancellationToken ct = default)
    {
        var subscribed = new List<string>();
        var adopted = new List<string>();
        var alreadyHealthy = new List<string>();
        var failed = new Dictionary<string, string>(StringComparer.Ordinal);

        var credential = await _credentialRepository.GetByUserIdAsync(userId, ct);
        if (credential == null)
        {
            _logger.LogWarning("EnsureSubscriptions: no Akahu credential for user {UserId}", userId);
            return new EnsureSubscriptionsResult(subscribed, adopted, alreadyHealthy, failed);
        }

        if (!TryDecryptTokens(credential, out var appIdToken, out var userToken))
        {
            _logger.LogWarning("EnsureSubscriptions: failed to decrypt Akahu tokens for user {UserId}", userId);
            return new EnsureSubscriptionsResult(subscribed, adopted, alreadyHealthy, failed);
        }

        var stateValue = userId.ToString("N");

        var localSubscriptions = await _subscriptionRepository.GetByUserIdAsync(userId, ct);
        var localByType = localSubscriptions
            .GroupBy(s => s.WebhookType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<AkahuWebhookSubscriptionInfo> remoteSubscriptions;
        try
        {
            remoteSubscriptions = await _akahuApiClient.ListWebhooksAsync(appIdToken, userToken, ct);
        }
        catch (Exception ex)
        {
            // Bail out: treating an unreachable Akahu as "remote has no subscriptions" would
            // tear down perfectly-good local rows. Retry on the next reconciliation cycle.
            _logger.LogWarning(ex, "EnsureSubscriptions: ListWebhooksAsync failed for user {UserId}; aborting reconcile until Akahu is reachable", userId);
            foreach (var webhookType in RequiredTypes)
            {
                failed[webhookType] = "Akahu webhooks endpoint unavailable; retrying next cycle";
            }
            return new EnsureSubscriptionsResult(subscribed, adopted, alreadyHealthy, failed);
        }

        var remoteByType = remoteSubscriptions
            .GroupBy(r => r.WebhookType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var webhookType in RequiredTypes)
        {
            try
            {
                localByType.TryGetValue(webhookType, out var localRow);
                remoteByType.TryGetValue(webhookType, out var remoteRow);

                if (localRow != null && remoteRow != null && string.Equals(localRow.WebhookId, remoteRow.Id, StringComparison.Ordinal))
                {
                    alreadyHealthy.Add(webhookType);
                    continue;
                }

                if (localRow != null && remoteRow == null)
                {
                    // Akahu no longer has the subscription — purge stale local row, then re-subscribe.
                    await _subscriptionRepository.DeleteByIdAsync(localRow.Id, ct);
                    var created = await _akahuApiClient.SubscribeToWebhookAsync(appIdToken, userToken, webhookType, stateValue, ct);
                    await PersistAsync(credential, userId, webhookType, created, stateValue, ct);
                    subscribed.Add(webhookType);
                    continue;
                }

                if (localRow == null && remoteRow != null)
                {
                    await PersistAsync(credential, userId, webhookType, remoteRow, stateValue, ct);
                    adopted.Add(webhookType);
                    continue;
                }

                if (localRow == null && remoteRow == null)
                {
                    var created = await _akahuApiClient.SubscribeToWebhookAsync(appIdToken, userToken, webhookType, stateValue, ct);
                    await PersistAsync(credential, userId, webhookType, created, stateValue, ct);
                    subscribed.Add(webhookType);
                    continue;
                }

                // Both rows exist but IDs disagree — drop the local one and adopt the remote.
                await _subscriptionRepository.DeleteByIdAsync(localRow!.Id, ct);
                await PersistAsync(credential, userId, webhookType, remoteRow!, stateValue, ct);
                adopted.Add(webhookType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "EnsureSubscriptions: failed to ensure Akahu {WebhookType} subscription for user {UserId}", webhookType, userId);
                failed[webhookType] = ex.Message;
            }
        }

        return new EnsureSubscriptionsResult(subscribed, adopted, alreadyHealthy, failed);
    }

    /// <inheritdoc />
    public async Task TearDownSubscriptionsAsync(Guid userId, CancellationToken ct = default)
    {
        var localSubscriptions = await _subscriptionRepository.GetByUserIdAsync(userId, ct);
        if (localSubscriptions.Count == 0)
            return;

        var credential = await _credentialRepository.GetByUserIdAsync(userId, ct);
        string? appIdToken = null;
        string? userToken = null;
        var hasTokens = credential != null && TryDecryptTokens(credential, out appIdToken, out userToken);

        if (hasTokens && appIdToken != null && userToken != null)
        {
            foreach (var subscription in localSubscriptions)
            {
                try
                {
                    await _akahuApiClient.UnsubscribeFromWebhookAsync(appIdToken, userToken, subscription.WebhookId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "TearDown: Akahu DELETE /webhooks/{WebhookId} failed for user {UserId}; continuing", subscription.WebhookId, userId);
                }
            }
        }

        await _subscriptionRepository.DeleteByUserIdAsync(userId, ct);
    }

    /// <inheritdoc />
    public async Task<ReconcileResult> ReconcileAsync(Guid userId, CancellationToken ct = default)
    {
        var ensure = await EnsureSubscriptionsAsync(userId, ct);
        return new ReconcileResult(ensure.SubscribedTypes, ensure.AdoptedTypes, ensure.AlreadyHealthyTypes, ensure.FailedTypes);
    }

    private async Task PersistAsync(
        AkahuUserCredential credential,
        Guid userId,
        string webhookType,
        AkahuWebhookSubscriptionInfo info,
        string stateValue,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var entity = new AkahuWebhookSubscription
        {
            UserId = userId,
            AkahuUserCredentialId = credential.Id,
            WebhookId = info.Id,
            WebhookType = string.IsNullOrEmpty(info.WebhookType) ? webhookType : info.WebhookType,
            State = info.State ?? stateValue,
            SubscribedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _subscriptionRepository.AddAsync(entity, ct);
    }

    private bool TryDecryptTokens(AkahuUserCredential credential, out string? appIdToken, out string? userToken)
    {
        appIdToken = null;
        userToken = null;

        try
        {
            var decryptedApp = _encryptionService.DecryptSettings<string>(credential.EncryptedAppToken);
            var decryptedUser = _encryptionService.DecryptSettings<string>(credential.EncryptedUserToken);

            if (string.IsNullOrEmpty(decryptedApp) || string.IsNullOrEmpty(decryptedUser))
                return false;

            appIdToken = decryptedApp;
            userToken = decryptedUser;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt Akahu credentials");
            return false;
        }
    }
}
