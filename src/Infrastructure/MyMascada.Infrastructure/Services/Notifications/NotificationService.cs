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
        // Idempotency check: if a groupKey is provided, skip if already exists
        if (!string.IsNullOrEmpty(groupKey))
        {
            var exists = await _notificationRepository.ExistsByGroupKeyAsync(userId, groupKey, cancellationToken);
            if (exists)
            {
                _logger.LogDebug("Skipping duplicate notification for user {UserId} with groupKey {GroupKey}", userId, groupKey);
                return;
            }
        }

        // Rate limiting: check daily count for this type
        var recentCount = await _notificationRepository.CountRecentByTypeAsync(
            userId, type, TimeSpan.FromDays(1), cancellationToken);
        if (recentCount >= MaxNotificationsPerTypePerDay)
        {
            _logger.LogDebug("Rate limit reached for user {UserId}, type {Type}. Skipping notification", userId, type);
            return;
        }

        // Check user preferences: skip if the user has explicitly disabled in-app for this type
        var preferences = await _preferenceRepository.GetByUserIdAsync(userId, cancellationToken);
        if (preferences?.ChannelPreferences != null)
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

        await _notificationRepository.CreateAsync(notification, cancellationToken);

        _logger.LogInformation("Created {Type} notification for user {UserId}", type, userId);

        // Future: dispatch to other delivery channels (push, email, etc.) based on preferences
    }
}
