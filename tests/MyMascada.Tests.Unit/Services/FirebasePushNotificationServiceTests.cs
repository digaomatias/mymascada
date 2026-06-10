using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Services.Push;

namespace MyMascada.Tests.Unit.Services;

public class FirebasePushNotificationServiceTests
{
    private readonly IUserDeviceRepository _deviceRepository = Substitute.For<IUserDeviceRepository>();
    private readonly IFcmClient _fcmClient = Substitute.For<IFcmClient>();
    private readonly FirebasePushNotificationService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public FirebasePushNotificationServiceTests()
    {
        _service = new FirebasePushNotificationService(
            _deviceRepository,
            _fcmClient,
            Substitute.For<ILogger<FirebasePushNotificationService>>());
    }

    private static UserDevice Device(Guid userId, string token) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        FcmToken = token,
        Platform = "ios",
        LastSeenAt = DateTime.UtcNow
    };

    [Fact]
    public async Task SendToUserAsync_FirebaseNotConfigured_SkipsWithoutQueryingDevices()
    {
        _fcmClient.IsConfigured.Returns(false);

        await _service.SendToUserAsync(_userId, "Title", "Body");

        await _deviceRepository.DidNotReceiveWithAnyArgs().GetByUserIdAsync(default, default);
        await _fcmClient.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task SendToUserAsync_DeliveryFailure_DoesNotThrow()
    {
        // Contract: push is best-effort and must never break the calling flow.
        _fcmClient.IsConfigured.Returns(true);
        _deviceRepository.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns<List<UserDevice>>(_ => throw new InvalidOperationException("database unavailable"));

        var act = () => _service.SendToUserAsync(_userId, "Title", "Body");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendToUserAsync_CallerCancellation_Propagates()
    {
        _fcmClient.IsConfigured.Returns(true);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _deviceRepository.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns<List<UserDevice>>(_ => throw new OperationCanceledException(cts.Token));

        var act = () => _service.SendToUserAsync(_userId, "Title", "Body", null, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SendToUserAsync_NoDevices_DoesNotSend()
    {
        _fcmClient.IsConfigured.Returns(true);
        _deviceRepository.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<UserDevice>());

        await _service.SendToUserAsync(_userId, "Title", "Body");

        await _fcmClient.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task SendToUserAsync_SendsToAllRegisteredDeviceTokens()
    {
        _fcmClient.IsConfigured.Returns(true);
        _deviceRepository.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<UserDevice> { Device(_userId, "token-a"), Device(_userId, "token-b") });
        _fcmClient.SendAsync(
                Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<FcmSendResult>
            {
                new() { Token = "token-a", Success = true },
                new() { Token = "token-b", Success = true }
            });

        await _service.SendToUserAsync(_userId, "Title", "Body",
            new Dictionary<string, string> { ["route"] = "/dashboard" });

        await _fcmClient.Received(1).SendAsync(
            Arg.Is<IReadOnlyList<string>>(t => t.Count == 2 && t.Contains("token-a") && t.Contains("token-b")),
            "Title",
            "Body",
            Arg.Is<IReadOnlyDictionary<string, string>>(d => d["route"] == "/dashboard"),
            Arg.Any<CancellationToken>());
        await _deviceRepository.DidNotReceiveWithAnyArgs().RemoveTokensAsync(default!, default);
    }

    [Fact]
    public async Task SendToUserAsync_PrunesTokensReportedAsUnregistered()
    {
        _fcmClient.IsConfigured.Returns(true);
        _deviceRepository.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<UserDevice>
            {
                Device(_userId, "valid-token"),
                Device(_userId, "stale-token"),
                Device(_userId, "transient-failure-token")
            });
        _fcmClient.SendAsync(
                Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<FcmSendResult>
            {
                new() { Token = "valid-token", Success = true },
                new() { Token = "stale-token", Success = false, IsTokenInvalid = true, Error = "Unregistered" },
                new() { Token = "transient-failure-token", Success = false, IsTokenInvalid = false, Error = "Unavailable" }
            });

        await _service.SendToUserAsync(_userId, "Title", "Body");

        // Only the unregistered token is pruned — transient failures keep their registration.
        await _deviceRepository.Received(1).RemoveTokensAsync(
            Arg.Is<IReadOnlyCollection<string>>(t => t.Count == 1 && t.Contains("stale-token")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendToUserAsync_DeduplicatesTokensBeforeSending()
    {
        _fcmClient.IsConfigured.Returns(true);
        _deviceRepository.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<UserDevice> { Device(_userId, "token-a"), Device(_userId, "token-a") });
        _fcmClient.SendAsync(
                Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<FcmSendResult> { new() { Token = "token-a", Success = true } });

        await _service.SendToUserAsync(_userId, "Title", "Body");

        await _fcmClient.Received(1).SendAsync(
            Arg.Is<IReadOnlyList<string>>(t => t.Count == 1),
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>());
    }
}
