using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Infrastructure.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationPreferenceRepository _preferenceRepository;
    private readonly ILogger<NotificationService> _logger;

    // Rate limit: max notifications per type per day
    private const int MaxNotificationsPerTypePerDay = 10;

    public NotificationService(
        INotificationRepository notificationRepository,
        INotificationPreferenceRepository preferenceRepository,
        ILogger<NotificationService> logger)
    {
        _notificationRepository = notificationRepository;
        _preferenceRepository = preferenceRepository;
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
            if (preferences.ChannelPreferences != null)
            {
                try
                {
                    var channelPrefs = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(
                        preferences.ChannelPreferences);
                    var typeName = type.ToString();
                    if (channelPrefs != null &&
                        channelPrefs.TryGetValue(typeName, out var typePrefs) &&
                        typePrefs.TryGetProperty("inApp", out var inAppProp) &&
                        inAppProp.ValueKind == System.Text.Json.JsonValueKind.False)
                    {
                        _logger.LogDebug("Skipping in-app notification for user {UserId}, type {Type} — disabled by user preference", userId, type);
                        return;
                    }
                }
                catch (System.Text.Json.JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse ChannelPreferences for user {UserId}; proceeding with notification delivery", userId);
                }
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

        // Future: dispatch to other delivery channels (push, email, etc.) based on preferences
    }
}
