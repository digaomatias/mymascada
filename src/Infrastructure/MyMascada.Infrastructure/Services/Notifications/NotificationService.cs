using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Services.Push;

namespace MyMascada.Infrastructure.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationPreferenceRepository _preferenceRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly ILogger<NotificationService> _logger;

    // Rate limit: max notifications per type per day
    private const int MaxNotificationsPerTypePerDay = 10;

    public NotificationService(
        INotificationRepository notificationRepository,
        INotificationPreferenceRepository preferenceRepository,
        IPushNotificationService pushNotificationService,
        ILogger<NotificationService> logger)
    {
        _notificationRepository = notificationRepository;
        _preferenceRepository = preferenceRepository;
        _pushNotificationService = pushNotificationService;
        _logger = logger;
    }

    public async Task CreateNotificationAsync(
        Guid userId,
        NotificationType type,
        string title,
        string body,
        string? data = null,
        NotificationPriority priority = NotificationPriority.Normal,
        string? groupKey = null,
        DateTime? expiresAt = null,
        bool periodDeduplicated = false,
        CancellationToken cancellationToken = default)
    {
        // periodDeduplicated relies entirely on the groupKey for dedup and skips the
        // per-type daily cap; without a groupKey it would have no duplicate
        // protection at all and could spam. Treat that combination as a misuse.
        if (periodDeduplicated && string.IsNullOrEmpty(groupKey))
            throw new ArgumentException(
                "periodDeduplicated notifications require a groupKey — it is the only dedup mechanism.",
                nameof(groupKey));

        // Idempotency pre-check (optimization — the real guard is the DB unique constraint on (UserId, GroupKey)).
        // A concurrent insert that races past this check will be caught by DbUpdateException in CreateAsync.
        // Period-deduplicated producers also count soft-deleted rows, so deleting an
        // alert from the bell doesn't un-suppress it for the rest of the period.
        if (!string.IsNullOrEmpty(groupKey))
        {
            var exists = await _notificationRepository.ExistsByGroupKeyAsync(
                userId, groupKey, includeDeleted: periodDeduplicated, cancellationToken);
            if (exists)
            {
                _logger.LogDebug("Skipping duplicate notification for user {UserId} with groupKey {GroupKey}", userId, groupKey);
                return;
            }
        }

        // Check user preferences: skip only if the user has explicitly disabled
        // in-app for this type.
        //
        // Quiet hours intentionally do NOT suppress in-app notification creation.
        // The in-app bell is a passive surface — dropping a notification here
        // would lose it permanently (no record, no groupKey), so a recurring
        // producer that always runs inside a user's quiet window (e.g. the daily
        // budget-alert job) would never deliver. Quiet hours should gate
        // *real-time* delivery (push/sound) instead — see the dispatch hook at
        // the end of this method, where QuietHoursStart/End must be consulted
        // once push is implemented.
        var preferences = await _preferenceRepository.GetByUserIdAsync(userId, cancellationToken);
        if (preferences != null)
        {

            // Enforce per-type channel preferences (inApp toggle).
            // KNOWN LIMITATION (intentional for this iteration): every push corresponds
            // to an in-app Notification row — the push payload deep-links to it via
            // notificationId — so disabling inApp for a type also suppresses push for
            // that type. "Push only, no in-app" is not supported.
            // See docs/push-notifications.md ("Known limitations").
            if (IsChannelExplicitlyDisabled(preferences.ChannelPreferences, type, "inApp", userId))
            {
                _logger.LogDebug("Skipping in-app notification (and its push counterpart) for user {UserId}, type {Type} — inApp disabled by user preference", userId, type);
                return;
            }
        }

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Priority = priority,
            Title = title,
            Body = body,
            Data = data,
            GroupKey = groupKey,
            ExpiresAt = expiresAt
        };

        Notification created;
        if (periodDeduplicated)
        {
            // Period-deduplicated fan-out (e.g. budget alerts): the per-type daily
            // cap would silently drop legitimate distinct alerts (one per category)
            // past the 10th. The groupKey check above (including soft-deleted) is
            // what bounds these, not the daily cap.
            created = await _notificationRepository.CreateAsync(notification, cancellationToken);
        }
        else
        {
            // Rate limiting: atomically check daily count and insert to prevent races.
            var rateLimited = await _notificationRepository.CreateIfRateLimitNotExceededAsync(
                notification, TimeSpan.FromDays(1), MaxNotificationsPerTypePerDay, cancellationToken);
            if (rateLimited == null)
            {
                _logger.LogDebug("Rate limit reached for user {UserId}, type {Type}. Skipping notification", userId, type);
                return;
            }
            created = rateLimited;
        }

        _logger.LogInformation("Created {Type} notification for user {UserId}", type, userId);

        // Dispatch to push channel unless the user explicitly disabled push for this type.
        // Push failures must never roll back the in-app notification, so this is fail-soft.
        if (!IsChannelExplicitlyDisabled(preferences?.ChannelPreferences, type, "push", userId))
        {
            await TrySendPushAsync(created, cancellationToken);
        }
        else
        {
            _logger.LogDebug("Skipping push notification for user {UserId}, type {Type} — disabled by user preference", userId, type);
        }
    }

    /// <summary>
    /// Sends the push counterpart of an in-app notification to the user's registered
    /// devices. Never throws — push delivery is best-effort.
    /// </summary>
    private async Task TrySendPushAsync(Notification notification, CancellationToken cancellationToken)
    {
        try
        {
            var (title, body) = PushContentFormatter.FormatContent(
                notification.Title, notification.Body, notification.Data);
            var data = PushContentFormatter.BuildDataPayload(notification.Type, notification.Id);

            await _pushNotificationService.SendToUserAsync(
                notification.UserId, title, body, data, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send push notification {NotificationId} to user {UserId}",
                notification.Id, notification.UserId);
        }
    }

    /// <summary>
    /// Returns true when the user's ChannelPreferences JSON explicitly disables the
    /// given channel ("inApp"/"push"/"email") for the notification type.
    /// Missing or malformed preferences default to enabled.
    /// </summary>
    private bool IsChannelExplicitlyDisabled(string? channelPreferencesJson, NotificationType type, string channel, Guid userId)
    {
        if (string.IsNullOrEmpty(channelPreferencesJson))
            return false;

        try
        {
            var channelPrefs = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(
                channelPreferencesJson);
            return channelPrefs != null &&
                   channelPrefs.TryGetValue(type.ToString(), out var typePrefs) &&
                   typePrefs.ValueKind == System.Text.Json.JsonValueKind.Object &&
                   typePrefs.TryGetProperty(channel, out var channelProp) &&
                   channelProp.ValueKind == System.Text.Json.JsonValueKind.False;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse ChannelPreferences for user {UserId}; proceeding with notification delivery", userId);
            return false;
        }
    }
}
