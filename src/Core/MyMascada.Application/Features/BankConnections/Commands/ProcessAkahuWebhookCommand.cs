using MediatR;
using MyMascada.Application.BackgroundJobs;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.BankConnections.Commands;

/// <summary>
/// MediatR command to process an incoming Akahu webhook event.
/// </summary>
public record ProcessAkahuWebhookCommand(AkahuWebhookPayload Payload) : IRequest;

public class ProcessAkahuWebhookCommandHandler : IRequestHandler<ProcessAkahuWebhookCommand>
{
    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly IBankSyncService _bankSyncService;
    private readonly IAkahuUserCredentialRepository _credentialRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAkahuWebhookSubscriptionRepository _subscriptionRepository;
    private readonly IAkahuMigrationJobService _migrationJobService;
    private readonly IApplicationLogger<ProcessAkahuWebhookCommandHandler> _logger;

    private const string AkahuProviderId = "akahu";

    public ProcessAkahuWebhookCommandHandler(
        IBankConnectionRepository bankConnectionRepository,
        IBankSyncService bankSyncService,
        IAkahuUserCredentialRepository credentialRepository,
        ITransactionRepository transactionRepository,
        IAkahuWebhookSubscriptionRepository subscriptionRepository,
        IAkahuMigrationJobService migrationJobService,
        IApplicationLogger<ProcessAkahuWebhookCommandHandler> logger)
    {
        _bankConnectionRepository = bankConnectionRepository;
        _bankSyncService = bankSyncService;
        _credentialRepository = credentialRepository;
        _transactionRepository = transactionRepository;
        _subscriptionRepository = subscriptionRepository;
        _migrationJobService = migrationJobService;
        _logger = logger;
    }

    public async Task Handle(ProcessAkahuWebhookCommand request, CancellationToken cancellationToken)
    {
        var payload = request.Payload;

        var safeItemId = payload.WebhookType == AkahuWebhookTypes.Token
            ? MaskSensitiveValue(payload.ItemId)
            : payload.ItemId;

        _logger.LogInformation(
            "Processing Akahu webhook: type={WebhookType}, code={WebhookCode}, itemId={ItemId}, state={State}",
            payload.WebhookType, payload.WebhookCode, safeItemId, payload.State);

        switch (payload.WebhookType)
        {
            case AkahuWebhookTypes.Token:
                await HandleTokenEventAsync(payload, cancellationToken);
                break;

            case AkahuWebhookTypes.Account:
                await HandleAccountEventAsync(payload, cancellationToken);
                break;

            case AkahuWebhookTypes.Transaction:
                await HandleTransactionEventAsync(payload, cancellationToken);
                break;

            default:
                _logger.LogWarning("Unknown Akahu webhook type: {WebhookType}", payload.WebhookType);
                break;
        }
    }

    private async Task HandleTokenEventAsync(AkahuWebhookPayload payload, CancellationToken ct)
    {
        if (payload.WebhookCode != AkahuWebhookCodes.Delete)
        {
            _logger.LogInformation("Ignoring TOKEN/{WebhookCode} event", payload.WebhookCode);
            return;
        }

        // Token was revoked — mark all bank connections for this user as disconnected
        if (!TryParseUserId(payload.State, out var userId))
        {
            _logger.LogWarning("TOKEN DELETE webhook missing or invalid state (user ID)");
            return;
        }

        _logger.LogInformation("Akahu token revoked for user {UserId}, marking connections as disconnected", userId);

        var connections = await _bankConnectionRepository.GetActiveByUserIdAsync(userId, ct);
        foreach (var connection in connections.Where(c => c.ProviderId == AkahuProviderId))
        {
            connection.IsActive = false;
            connection.LastSyncError = "Akahu token was revoked";
            await _bankConnectionRepository.UpdateAsync(connection, ct);
        }

        // Clean up stored credentials
        await _credentialRepository.DeleteByUserIdAsync(userId, ct);

        // Akahu cancels the subscription on its end when the token is revoked, so we only need
        // to purge the local rows — no DELETE /webhooks API call is necessary.
        await _subscriptionRepository.DeleteByUserIdAsync(userId, ct);
    }

    private async Task HandleAccountEventAsync(AkahuWebhookPayload payload, CancellationToken ct)
    {
        switch (payload.WebhookCode)
        {
            case AkahuWebhookCodes.Update:
                await HandleAccountUpdateAsync(payload, ct);
                break;

            case AkahuWebhookCodes.Delete:
                await HandleAccountDeleteAsync(payload, ct);
                break;

            case AkahuWebhookCodes.Create:
                _logger.LogInformation("New Akahu account {AccountId} connected — will be picked up on next sync", payload.ItemId);
                break;

            case AkahuWebhookCodes.Migrate:
                await HandleAccountMigrateAsync(payload, ct);
                break;

            case AkahuWebhookCodes.WebhookCancelled:
                await HandleWebhookCancelledAsync(payload, AkahuWebhookTypes.Account, ct);
                break;

            default:
                _logger.LogInformation("Ignoring ACCOUNT/{WebhookCode} event", payload.WebhookCode);
                break;
        }
    }

    private async Task HandleAccountMigrateAsync(AkahuWebhookPayload payload, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(payload.PreviousItemId))
        {
            _logger.LogWarning("ACCOUNT/MIGRATE webhook missing previous_item_id");
            return;
        }

        var connection = await _bankConnectionRepository.GetByExternalAccountIdAsync(payload.PreviousItemId, AkahuProviderId, ct);
        if (connection == null)
        {
            _logger.LogWarning(
                "ACCOUNT/MIGRATE webhook for unknown classic account {PreviousItemId}; will catch up on next sync",
                payload.PreviousItemId);
            return;
        }

        _logger.LogInformation(
            "ACCOUNT/MIGRATE webhook: enqueueing migration job for connection {ConnectionId} (user {UserId})",
            connection.Id, connection.UserId);

        try
        {
            // Fire-and-forget via Hangfire so this webhook handler responds within Akahu's
            // timeout. The job retries automatically on failure.
            var jobId = _migrationJobService.EnqueueMigration(connection.UserId, connection.Id);
            _logger.LogInformation(
                "ACCOUNT/MIGRATE enqueued job {JobId} for connection {ConnectionId}",
                jobId, connection.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ACCOUNT/MIGRATE failed to enqueue migration job for connection {ConnectionId}", connection.Id);
        }

        await Task.CompletedTask;
    }

    private async Task HandleWebhookCancelledAsync(AkahuWebhookPayload payload, string webhookType, CancellationToken ct)
    {
        if (!TryParseUserId(payload.State, out var userId))
        {
            _logger.LogWarning("WEBHOOK_CANCELLED event missing or invalid state");
            return;
        }

        var subscriptions = await _subscriptionRepository.GetByUserIdAsync(userId, ct);
        var match = subscriptions.FirstOrDefault(s => string.Equals(s.WebhookType, webhookType, StringComparison.OrdinalIgnoreCase));
        if (match == null)
        {
            _logger.LogInformation("WEBHOOK_CANCELLED for user {UserId} type {WebhookType}: no local row to delete", userId, webhookType);
            return;
        }

        _logger.LogInformation(
            "WEBHOOK_CANCELLED for user {UserId} type {WebhookType}: deleting local subscription row {Id} so reconciliation can re-create it",
            userId, webhookType, match.Id);

        await _subscriptionRepository.DeleteByIdAsync(match.Id, ct);
    }

    private async Task HandleAccountUpdateAsync(AkahuWebhookPayload payload, CancellationToken ct)
    {
        if (payload.UpdatedFields == null || !payload.UpdatedFields.Contains("balance"))
        {
            _logger.LogDebug("ACCOUNT UPDATE for {AccountId} does not include balance change, skipping", payload.ItemId);
            return;
        }

        var connection = await FindConnectionByExternalIdAsync(payload.ItemId, ct);
        if (connection == null)
            return;

        _logger.LogInformation("Balance updated for Akahu account {AccountId}, triggering sync for connection {ConnectionId}",
            payload.ItemId, connection.Id);

        await _bankSyncService.SyncAccountAsync(connection.Id, BankSyncType.Webhook, ct);
    }

    private async Task HandleAccountDeleteAsync(AkahuWebhookPayload payload, CancellationToken ct)
    {
        var connection = await FindConnectionByExternalIdAsync(payload.ItemId, ct);
        if (connection == null)
            return;

        _logger.LogInformation("Akahu account {AccountId} deleted, marking connection {ConnectionId} as disconnected",
            payload.ItemId, connection.Id);

        connection.IsActive = false;
        connection.LastSyncError = "Akahu account was deleted";
        await _bankConnectionRepository.UpdateAsync(connection, ct);
    }

    private async Task HandleTransactionEventAsync(AkahuWebhookPayload payload, CancellationToken ct)
    {
        switch (payload.WebhookCode)
        {
            case AkahuWebhookCodes.InitialUpdate:
            case AkahuWebhookCodes.DefaultUpdate:
                await HandleTransactionUpdateAsync(payload, ct);
                break;

            case AkahuWebhookCodes.Delete:
                await HandleTransactionDeleteAsync(payload, ct);
                break;

            case AkahuWebhookCodes.WebhookCancelled:
                await HandleWebhookCancelledAsync(payload, AkahuWebhookTypes.Transaction, ct);
                break;

            default:
                _logger.LogInformation("Ignoring TRANSACTION/{WebhookCode} event", payload.WebhookCode);
                break;
        }
    }

    private async Task HandleTransactionUpdateAsync(AkahuWebhookPayload payload, CancellationToken ct)
    {
        var syncType = payload.WebhookCode == AkahuWebhookCodes.InitialUpdate
            ? BankSyncType.Initial
            : BankSyncType.Webhook;

        var connection = await FindConnectionByExternalIdAsync(payload.ItemId, ct);
        if (connection == null)
            return;

        _logger.LogInformation(
            "Akahu transaction update ({Code}) for account {AccountId}: {Count} new transactions, triggering sync for connection {ConnectionId}",
            payload.WebhookCode, payload.ItemId, payload.NewTransactions ?? 0, connection.Id);

        await _bankSyncService.SyncAccountAsync(connection.Id, syncType, ct);
    }

    private async Task HandleTransactionDeleteAsync(AkahuWebhookPayload payload, CancellationToken ct)
    {
        if (payload.RemovedTransactions == null || payload.RemovedTransactions.Length == 0)
        {
            _logger.LogDebug("TRANSACTION DELETE webhook had no removed_transactions");
            return;
        }

        _logger.LogInformation("Akahu transaction delete: removing {Count} transactions", payload.RemovedTransactions.Length);

        await _transactionRepository.DeleteByExternalIdsAsync(payload.RemovedTransactions, ct);
    }

    private async Task<Domain.Entities.BankConnection?> FindConnectionByExternalIdAsync(string? externalAccountId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(externalAccountId))
        {
            _logger.LogWarning("Webhook event missing item_id (external account ID)");
            return null;
        }

        var connection = await _bankConnectionRepository.GetByExternalAccountIdAsync(externalAccountId, AkahuProviderId, ct);
        if (connection == null)
        {
            _logger.LogWarning("No bank connection found for Akahu account {AccountId}", externalAccountId);
        }
        return connection;
    }

    private static bool TryParseUserId(string? state, out Guid userId)
    {
        userId = Guid.Empty;
        return !string.IsNullOrEmpty(state) && Guid.TryParse(state, out userId);
    }

    private static string MaskSensitiveValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "[empty]";
        if (value.Length <= 4)
            return "***";
        return $"***{value[^4..]}";
    }
}
