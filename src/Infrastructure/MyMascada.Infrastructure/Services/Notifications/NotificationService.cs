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

        // Check user preferences (is this type enabled for in-app?)
        var preferences = await _preferenceRepository.GetByUserIdAsync(userId, cancellationToken);
        if (preferences?.ChannelPreferences != null)
        {
            // For now, channel preferences are stored as JSON but we only check for in-app.
            // If the user has explicitly disabled in-app for this type, skip.
            // This is a simplified check — a full implementation would parse the JSON.
            // By default, all types are enabled for in-app delivery.
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
