using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Infrastructure.Services.Push;

/// <summary>
/// Sends push notifications to all of a user's registered devices via FCM and
/// prunes tokens that FCM reports as unregistered/invalid.
/// </summary>
public class FirebasePushNotificationService : IPushNotificationService
{
    private readonly IUserDeviceRepository _deviceRepository;
    private readonly IFcmClient _fcmClient;
    private readonly ILogger<FirebasePushNotificationService> _logger;

    public FirebasePushNotificationService(
        IUserDeviceRepository deviceRepository,
        IFcmClient fcmClient,
        ILogger<FirebasePushNotificationService> logger)
    {
        _deviceRepository = deviceRepository;
        _fcmClient = fcmClient;
        _logger = logger;
    }

    public async Task SendToUserAsync(
        Guid userId,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        // Push delivery is best-effort and must never break the calling flow
        // (see IPushNotificationService contract). Only cancellation propagates.
        try
        {
            await SendToUserCoreAsync(userId, title, body, data, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send push notification to user {UserId}", userId);
        }
    }

    private async Task SendToUserCoreAsync(
        Guid userId,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken cancellationToken)
    {
        if (!_fcmClient.IsConfigured)
        {
            _logger.LogDebug("Skipping push for user {UserId} — Firebase is not configured", userId);
            return;
        }

        var devices = await _deviceRepository.GetByUserIdAsync(userId, cancellationToken);
        if (devices.Count == 0)
        {
            _logger.LogDebug("Skipping push for user {UserId} — no registered devices", userId);
            return;
        }

        var tokens = devices.Select(d => d.FcmToken).Distinct().ToList();
        var results = await _fcmClient.SendAsync(
            tokens, title, body, data ?? new Dictionary<string, string>(), cancellationToken);

        var invalidTokens = results
            .Where(r => !r.Success && r.IsTokenInvalid)
            .Select(r => r.Token)
            .ToList();

        if (invalidTokens.Count > 0)
        {
            _logger.LogInformation(
                "Pruning {Count} unregistered FCM token(s) for user {UserId}", invalidTokens.Count, userId);
            await _deviceRepository.RemoveTokensAsync(invalidTokens, cancellationToken);
        }

        var successCount = results.Count(r => r.Success);
        var transientFailures = results.Count(r => !r.Success && !r.IsTokenInvalid);
        if (transientFailures > 0)
        {
            _logger.LogWarning(
                "Push to user {UserId}: {Success}/{Total} delivered, {Failures} transient failure(s)",
                userId, successCount, tokens.Count, transientFailures);
        }
        else
        {
            _logger.LogInformation(
                "Push to user {UserId}: {Success}/{Total} delivered", userId, successCount, tokens.Count);
        }
    }
}
