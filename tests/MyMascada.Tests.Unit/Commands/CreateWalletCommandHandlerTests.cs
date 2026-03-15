using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Wallets.Commands;
using MyMascada.Application.Features.Wallets.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Tests.Unit.Commands;

public class CreateWalletCommandHandlerTests
{
    private readonly IWalletRepository _walletRepository;
    private readonly CreateWalletCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public CreateWalletCommandHandlerTests()
    {
        _walletRepository = Substitute.For<IWalletRepository>();
        _handler = new CreateWalletCommandHandler(_walletRepository);
    }

    [Fact]
    public async Task Handle_WithValidRequest_ShouldCreateWalletAndReturnDto()
    {
        // Arrange
        var command = new CreateWalletCommand
        {
            Name = "Vacation Fund",
            Icon = "plane",
            Color = "#FF5733",
            Currency = "usd",
            TargetAmount = 5000m,
            UserId = _userId
        };

        _walletRepository.WalletNameExistsAsync(_userId, "Vacation Fund", null, Arg.Any<CancellationToken>())
            .Returns(false);

        _walletRepository.CreateWalletAsync(Arg.Any<Wallet>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var wallet = callInfo.Arg<Wallet>();
                wallet.Id = 1;
                wallet.Allocations = new List<WalletAllocation>();
                return wallet;
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<WalletDetailDto>();
        result.Id.Should().Be(1);
        result.Name.Should().Be("Vacation Fund");
        result.Icon.Should().Be("plane");
        result.Color.Should().Be("#FF5733");
        result.Currency.Should().Be("USD");
        result.TargetAmount.Should().Be(5000m);
        result.IsArchived.Should().BeFalse();
        result.Balance.Should().Be(0m);
        result.AllocationCount.Should().Be(0);

        await _walletRepository.Received(1).CreateWalletAsync(
            Arg.Is<Wallet>(w =>
                w.Name == "Vacation Fund" &&
                w.Currency == "USD" &&
                w.UserId == _userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange
        var command = new CreateWalletCommand
        {
            Name = "",
            Currency = "USD",
            UserId = _userId
        };

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Wallet name is required.");

        await _walletRepository.DidNotReceive().CreateWalletAsync(Arg.Any<Wallet>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithWhitespaceName_ShouldThrowArgumentException()
    {
        // Arrange
        var command = new CreateWalletCommand
        {
            Name = "   ",
            Currency = "USD",
            UserId = _userId
        };

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Wallet name is required.");

        await _walletRepository.DidNotReceive().CreateWalletAsync(Arg.Any<Wallet>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDuplicateName_ShouldThrowArgumentException()
    {
        // Arrange
        var command = new CreateWalletCommand
        {
            Name = "Savings",
            Currency = "USD",
            UserId = _userId
        };

        _walletRepository.WalletNameExistsAsync(_userId, "Savings", null, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("A wallet with the name 'Savings' already exists.");

        await _walletRepository.DidNotReceive().CreateWalletAsync(Arg.Any<Wallet>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithLeadingAndTrailingSpaces_ShouldTrimNameBeforeSaving()
    {
        // Arrange
        var command = new CreateWalletCommand
        {
            Name = "  Emergency Fund  ",
            Currency = "USD",
            UserId = _userId
        };

        _walletRepository.WalletNameExistsAsync(_userId, "Emergency Fund", null, Arg.Any<CancellationToken>())
            .Returns(false);

        _walletRepository.CreateWalletAsync(Arg.Any<Wallet>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var wallet = callInfo.Arg<Wallet>();
                wallet.Id = 1;
                wallet.Allocations = new List<WalletAllocation>();
                return wallet;
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Name.Should().Be("Emergency Fund");

        await _walletRepository.Received(1).CreateWalletAsync(
            Arg.Is<Wallet>(w => w.Name == "Emergency Fund"),
            Arg.Any<CancellationToken>());
    }
}
