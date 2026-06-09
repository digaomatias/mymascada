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
        CancellationToken cancellationToken = default)
    {
        // Idempotency pre-check (optimization — the real guard is the DB unique constraint on (UserId, GroupKey)).
        // A concurrent insert that races past this check will be caught by DbUpdateException in CreateAsync.
        if (!string.IsNullOrEmpty(groupKey))
        {
            var exists = await _notificationRepository.ExistsByGroupKeyAsync(userId, groupKey, cancellationToken);
            if (exists)
            {
                _logger.LogDebug("Skipping duplicate notification for user {UserId} with groupKey {GroupKey}", userId, groupKey);
                return;
            }
        }

        // Check user preferences: skip if the user has explicitly disabled in-app for this type, or if quiet hours are active
        var preferences = await _preferenceRepository.GetByUserIdAsync(userId, cancellationToken);
        if (preferences != null)
        {
            // Enforce quiet hours
            if (preferences.QuietHoursStart.HasValue && preferences.QuietHoursEnd.HasValue)
            {
                try
                {
                    var tz = string.IsNullOrWhiteSpace(preferences.QuietHoursTimezone)
                        ? TimeZoneInfo.Utc
                        : TimeZoneInfo.FindSystemTimeZoneById(preferences.QuietHoursTimezone);
                    var userNow = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
                    var start = preferences.QuietHoursStart.Value;
                    var end = preferences.QuietHoursEnd.Value;
                    var inQuietHours = start <= end
                        ? userNow >= start && userNow < end         // same-day window e.g. 22:00–23:59
                        : userNow >= start || userNow < end;        // overnight window e.g. 22:00–08:00
                    if (inQuietHours)
                    {
                        _logger.LogDebug("Skipping notification for user {UserId} — currently in quiet hours", userId);
                        return;
                    }
                }
                catch (TimeZoneNotFoundException ex)
                {
                    _logger.LogWarning(ex, "Unknown timezone '{Timezone}' in quiet hours preference for user {UserId}; skipping quiet hours check", preferences.QuietHoursTimezone, userId);
                }
            }

            // Enforce per-type channel preferences (inApp toggle)
            if (IsChannelExplicitlyDisabled(preferences.ChannelPreferences, type, "inApp", userId))
            {
                _logger.LogDebug("Skipping in-app notification for user {UserId}, type {Type} — disabled by user preference", userId, type);
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

        // Rate limiting: atomically check daily count and insert to prevent races.
        var created = await _notificationRepository.CreateIfRateLimitNotExceededAsync(
            notification, TimeSpan.FromDays(1), MaxNotificationsPerTypePerDay, cancellationToken);
        if (created == null)
        {
            _logger.LogDebug("Rate limit reached for user {UserId}, type {Type}. Skipping notification", userId, type);
            return;
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
