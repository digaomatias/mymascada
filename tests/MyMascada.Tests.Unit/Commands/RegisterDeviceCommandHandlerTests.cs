using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Devices.Commands;
using MyMascada.Domain.Entities;

namespace MyMascada.Tests.Unit.Commands;

public class RegisterDeviceCommandHandlerTests
{
    private readonly IUserDeviceRepository _repository = Substitute.For<IUserDeviceRepository>();
    private readonly RegisterDeviceCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public RegisterDeviceCommandHandlerTests()
    {
        _handler = new RegisterDeviceCommandHandler(_repository);
    }

    private static UserDevice MakeDevice(Guid userId, string token, string platform) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        FcmToken = token,
        Platform = platform,
        LastSeenAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Handle_ValidRequest_UpsertsAndReturnsDeviceDto()
    {
        var device = MakeDevice(_userId, "fcm-token-123", "ios");
        _repository.UpsertAsync(_userId, "fcm-token-123", "ios", Arg.Any<CancellationToken>())
            .Returns(device);

        var result = await _handler.Handle(new RegisterDeviceCommand
        {
            UserId = _userId,
            Token = "fcm-token-123",
            Platform = "ios"
        }, CancellationToken.None);

        result.Id.Should().Be(device.Id);
        result.Platform.Should().Be("ios");
        result.LastSeenAt.Should().Be(device.LastSeenAt);
        await _repository.Received(1).UpsertAsync(_userId, "fcm-token-123", "ios", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TrimsTokenAndNormalizesPlatformCasing()
    {
        var device = MakeDevice(_userId, "fcm-token-123", "android");
        _repository.UpsertAsync(_userId, "fcm-token-123", "android", Arg.Any<CancellationToken>())
            .Returns(device);

        await _handler.Handle(new RegisterDeviceCommand
        {
            UserId = _userId,
            Token = "  fcm-token-123  ",
            Platform = "Android"
        }, CancellationToken.None);

        await _repository.Received(1).UpsertAsync(_userId, "fcm-token-123", "android", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_MissingToken_ThrowsArgumentException(string token)
    {
        var act = () => _handler.Handle(new RegisterDeviceCommand
        {
            UserId = _userId,
            Token = token,
            Platform = "ios"
        }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*token*");
        await _repository.DidNotReceiveWithAnyArgs().UpsertAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task Handle_TokenTooLong_ThrowsArgumentException()
    {
        var act = () => _handler.Handle(new RegisterDeviceCommand
        {
            UserId = _userId,
            Token = new string('x', RegisterDeviceCommandHandler.MaxTokenLength + 1),
            Platform = "ios"
        }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*maximum length*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("windows")]
    [InlineData("web")]
    public async Task Handle_InvalidPlatform_ThrowsArgumentException(string platform)
    {
        var act = () => _handler.Handle(new RegisterDeviceCommand
        {
            UserId = _userId,
            Token = "fcm-token-123",
            Platform = platform
        }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Platform*");
        await _repository.DidNotReceiveWithAnyArgs().UpsertAsync(default, default!, default!, default);
    }
}
