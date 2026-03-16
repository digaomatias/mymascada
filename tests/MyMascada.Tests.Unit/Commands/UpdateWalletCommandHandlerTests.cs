using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Wallets.Commands;
using MyMascada.Application.Features.Wallets.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Tests.Unit.Commands;

public class UpdateWalletCommandHandlerTests
{
    private readonly IWalletRepository _walletRepository;
    private readonly UpdateWalletCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public UpdateWalletCommandHandlerTests()
    {
        _walletRepository = Substitute.For<IWalletRepository>();
        _handler = new UpdateWalletCommandHandler(_walletRepository);
    }

    [Fact]
    public async Task Handle_WhenChangingCurrencyWithAllocations_ShouldThrowArgumentException()
    {
        // Arrange
        var walletId = 1;
        var command = new UpdateWalletCommand
        {
            WalletId = walletId,
            Currency = "EUR",
            UserId = _userId
        };

        var wallet = new Wallet
        {
            Id = walletId,
            Name = "Travel Fund",
            Currency = "USD",
            UserId = _userId,
            Allocations = new List<WalletAllocation>()
        };

        var existingAllocations = new List<WalletAllocation>
        {
            new() { Id = 1, WalletId = walletId, Amount = 100m }
        };

        _walletRepository.GetWalletByIdAsync(walletId, _userId, Arg.Any<CancellationToken>())
            .Returns(wallet);

        _walletRepository.GetAllocationsForWalletAsync(walletId, Arg.Any<CancellationToken>())
            .Returns(existingAllocations.AsEnumerable());

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Cannot change currency on a wallet that has existing allocations.");

        await _walletRepository.DidNotReceive().UpdateWalletAsync(Arg.Any<Wallet>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenChangingCurrencyWithNoAllocations_ShouldSucceed()
    {
        // Arrange
        var walletId = 1;
        var command = new UpdateWalletCommand
        {
            WalletId = walletId,
            Currency = "EUR",
            UserId = _userId
        };

        var wallet = new Wallet
        {
            Id = walletId,
            Name = "Travel Fund",
            Currency = "USD",
            UserId = _userId,
            Allocations = new List<WalletAllocation>()
        };

        _walletRepository.GetWalletByIdAsync(walletId, _userId, Arg.Any<CancellationToken>())
            .Returns(wallet);

        _walletRepository.GetAllocationsForWalletAsync(walletId, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<WalletAllocation>());

        _walletRepository.UpdateWalletAsync(Arg.Any<Wallet>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var w = callInfo.Arg<Wallet>();
                w.Allocations = new List<WalletAllocation>();
                return w;
            });

        _walletRepository.GetWalletBalanceAsync(walletId, Arg.Any<CancellationToken>())
            .Returns(0m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Currency.Should().Be("EUR");

        await _walletRepository.Received(1).UpdateWalletAsync(
            Arg.Is<Wallet>(w => w.Currency == "EUR"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenSettingSameCurrency_ShouldNotCheckAllocations()
    {
        // Arrange
        var walletId = 1;
        var command = new UpdateWalletCommand
        {
            WalletId = walletId,
            Currency = "usd", // Same as existing, different case
            UserId = _userId
        };

        var wallet = new Wallet
        {
            Id = walletId,
            Name = "Savings",
            Currency = "USD",
            UserId = _userId,
            Allocations = new List<WalletAllocation>()
        };

        _walletRepository.GetWalletByIdAsync(walletId, _userId, Arg.Any<CancellationToken>())
            .Returns(wallet);

        _walletRepository.UpdateWalletAsync(Arg.Any<Wallet>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var w = callInfo.Arg<Wallet>();
                w.Allocations = new List<WalletAllocation>();
                return w;
            });

        _walletRepository.GetWalletBalanceAsync(walletId, Arg.Any<CancellationToken>())
            .Returns(500m);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Currency.Should().Be("USD");

        // Should NOT have called GetAllocationsForWalletAsync since currency didn't actually change
        await _walletRepository.DidNotReceive().GetAllocationsForWalletAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenWalletNotFound_ShouldThrowArgumentException()
    {
        // Arrange
        var command = new UpdateWalletCommand
        {
            WalletId = 999,
            Name = "New Name",
            UserId = _userId
        };

        _walletRepository.GetWalletByIdAsync(999, _userId, Arg.Any<CancellationToken>())
            .Returns((Wallet?)null);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Wallet not found or you don't have permission to access it.");

        await _walletRepository.DidNotReceive().UpdateWalletAsync(Arg.Any<Wallet>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidCurrencyCode_ShouldThrowArgumentException()
    {
        // Arrange
        var walletId = 1;
        var command = new UpdateWalletCommand
        {
            WalletId = walletId,
            Currency = "ABCD", // Invalid: 4 characters
            UserId = _userId
        };

        var wallet = new Wallet
        {
            Id = walletId,
            Name = "Savings",
            Currency = "USD",
            UserId = _userId,
            Allocations = new List<WalletAllocation>()
        };

        _walletRepository.GetWalletByIdAsync(walletId, _userId, Arg.Any<CancellationToken>())
            .Returns(wallet);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Currency must be a 3-letter code.");

        await _walletRepository.DidNotReceive().UpdateWalletAsync(Arg.Any<Wallet>(), Arg.Any<CancellationToken>());
    }
}
