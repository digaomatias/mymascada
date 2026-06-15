using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Devices.Commands;

namespace MyMascada.Tests.Unit.Commands;

public class UnregisterDeviceCommandHandlerTests
{
    private readonly IUserDeviceRepository _repository = Substitute.For<IUserDeviceRepository>();
    private readonly UnregisterDeviceCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public UnregisterDeviceCommandHandlerTests()
    {
        _handler = new UnregisterDeviceCommandHandler(_repository);
    }

    [Fact]
    public async Task Handle_ValidToken_DeletesRegistration()
    {
        await _handler.Handle(new UnregisterDeviceCommand
        {
            UserId = _userId,
            Token = "fcm-token-123"
        }, CancellationToken.None);

        await _repository.Received(1).DeleteByTokenAsync(_userId, "fcm-token-123", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TrimsToken()
    {
        await _handler.Handle(new UnregisterDeviceCommand
        {
            UserId = _userId,
            Token = "  fcm-token-123 "
        }, CancellationToken.None);

        await _repository.Received(1).DeleteByTokenAsync(_userId, "fcm-token-123", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_MissingToken_ThrowsArgumentException(string token)
    {
        var act = () => _handler.Handle(new UnregisterDeviceCommand
        {
            UserId = _userId,
            Token = token
        }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        await _repository.DidNotReceiveWithAnyArgs().DeleteByTokenAsync(default, default!, default);
    }
}
