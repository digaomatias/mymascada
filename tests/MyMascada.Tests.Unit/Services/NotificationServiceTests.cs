using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Services.Notifications;

namespace MyMascada.Tests.Unit.Services;

public class NotificationServiceTests
{
    private readonly INotificationRepository _notificationRepo = Substitute.For<INotificationRepository>();
    private readonly INotificationPreferenceRepository _preferenceRepo = Substitute.For<INotificationPreferenceRepository>();
    private readonly NotificationService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public NotificationServiceTests()
    {
        _sut = new NotificationService(_notificationRepo, _preferenceRepo, Substitute.For<ILogger<NotificationService>>());
        // Default: not a duplicate, and the rate-limited create succeeds.
        _notificationRepo.ExistsByGroupKeyAsync(_userId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _notificationRepo.CreateIfRateLimitNotExceededAsync(
                Arg.Any<Notification>(), Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Notification>());
    }

    private Task Act() => _sut.CreateNotificationAsync(
        _userId, NotificationType.BudgetThreshold, "title", "body", groupKey: "gk-1");

    [Fact]
    public async Task CreateNotification_DuringQuietHours_StillCreatesInAppNotification()
    {
        // Quiet hours cover the entire day, so "now" is always inside the window.
        // The in-app notification must still be created — quiet hours only gate
        // real-time push delivery (not yet implemented), not the passive bell.
        _preferenceRepo.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(new NotificationPreference
        {
            QuietHoursStart = new TimeOnly(0, 0),
            QuietHoursEnd = new TimeOnly(23, 59),
            QuietHoursTimezone = "UTC"
        });

        await Act();

        await _notificationRepo.Received(1).CreateIfRateLimitNotExceededAsync(
            Arg.Any<Notification>(), Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateNotification_NoPreferences_CreatesNotification()
    {
        _preferenceRepo.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns((NotificationPreference?)null);

        await Act();

        await _notificationRepo.Received(1).CreateIfRateLimitNotExceededAsync(
            Arg.Any<Notification>(), Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateNotification_InAppDisabledForType_DoesNotCreate()
    {
        // The per-type in-app toggle must still suppress creation — only the
        // quiet-hours behavior changed.
        _preferenceRepo.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(new NotificationPreference
        {
            ChannelPreferences = """{"BudgetThreshold":{"inApp":false}}"""
        });

        await Act();

        await _notificationRepo.DidNotReceive().CreateIfRateLimitNotExceededAsync(
            Arg.Any<Notification>(), Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateNotification_DuplicateGroupKey_DoesNotCreate()
    {
        _notificationRepo.ExistsByGroupKeyAsync(_userId, "gk-1", Arg.Any<CancellationToken>()).Returns(true);

        await Act();

        await _notificationRepo.DidNotReceive().CreateIfRateLimitNotExceededAsync(
            Arg.Any<Notification>(), Arg.Any<TimeSpan>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
