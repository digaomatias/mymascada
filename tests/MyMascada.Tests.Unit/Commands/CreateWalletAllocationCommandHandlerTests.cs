using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Wallets.Commands;
using MyMascada.Application.Features.Wallets.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Tests.Unit.Commands;

public class CreateWalletAllocationCommandHandlerTests
{
    private readonly IWalletRepository _walletRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountAccessService _accountAccessService;
    private readonly CreateWalletAllocationCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public CreateWalletAllocationCommandHandlerTests()
    {
        _walletRepository = Substitute.For<IWalletRepository>();
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _accountAccessService = Substitute.For<IAccountAccessService>();
        _handler = new CreateWalletAllocationCommandHandler(
            _walletRepository,
            _transactionRepository,
            _accountAccessService);
    }

    [Fact]
    public async Task Handle_WithValidRequest_ShouldCreateAllocationAndReturnDto()
    {
        // Arrange
        var walletId = 1;
        var transactionId = 10;
        var accountId = 5;

        var command = new CreateWalletAllocationCommand
        {
            WalletId = walletId,
            TransactionId = transactionId,
            Amount = 150.00m,
            Note = "Monthly savings",
            UserId = _userId
        };

        var wallet = new Wallet
        {
            Id = walletId,
            Name = "Savings",
            UserId = _userId,
            Allocations = new List<WalletAllocation>()
        };

        var transaction = new Transaction
        {
            Id = transactionId,
            AccountId = accountId,
            Description = "Salary deposit",
            TransactionDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Amount = 3000m,
            Account = new Account { Id = accountId, Name = "Checking" }
        };

        var createdAllocation = new WalletAllocation
        {
            Id = 100,
            WalletId = walletId,
            TransactionId = transactionId,
            Amount = 150.00m,
            Note = "Monthly savings",
            Transaction = transaction
        };

        _walletRepository.GetWalletByIdAsync(walletId, _userId, Arg.Any<CancellationToken>())
            .Returns(wallet);

        _transactionRepository.GetByIdAsync(transactionId)
            .Returns(transaction);

        _accountAccessService.CanAccessAccountAsync(_userId, accountId)
            .Returns(true);

        _walletRepository.CreateAllocationAsync(Arg.Any<WalletAllocation>(), Arg.Any<CancellationToken>())
            .Returns(createdAllocation);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<WalletAllocationDto>();
        result.Id.Should().Be(100);
        result.TransactionId.Should().Be(transactionId);
        result.TransactionDescription.Should().Be("Salary deposit");
        result.Amount.Should().Be(150.00m);
        result.Note.Should().Be("Monthly savings");
        result.AccountName.Should().Be("Checking");

        await _walletRepository.Received(1).CreateAllocationAsync(
            Arg.Is<WalletAllocation>(a =>
                a.WalletId == walletId &&
                a.TransactionId == transactionId &&
                a.Amount == 150.00m &&
                a.Note == "Monthly savings"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenWalletNotFound_ShouldThrowArgumentException()
    {
        // Arrange
        var command = new CreateWalletAllocationCommand
        {
            WalletId = 999,
            TransactionId = 1,
            Amount = 50m,
            UserId = _userId
        };

        _walletRepository.GetWalletByIdAsync(999, _userId, Arg.Any<CancellationToken>())
            .Returns((Wallet?)null);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Wallet not found or you don't have permission to access it.");

        await _transactionRepository.DidNotReceive().GetByIdAsync(Arg.Any<int>());
        await _walletRepository.DidNotReceive().CreateAllocationAsync(Arg.Any<WalletAllocation>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserCannotAccessTransactionAccount_ShouldThrowArgumentException()
    {
        // Arrange
        var walletId = 1;
        var transactionId = 10;
        var accountId = 5;

        var command = new CreateWalletAllocationCommand
        {
            WalletId = walletId,
            TransactionId = transactionId,
            Amount = 50m,
            UserId = _userId
        };

        var wallet = new Wallet
        {
            Id = walletId,
            Name = "Savings",
            UserId = _userId,
            Allocations = new List<WalletAllocation>()
        };

        var transaction = new Transaction
        {
            Id = transactionId,
            AccountId = accountId,
            Description = "Some transaction",
            Amount = 100m
        };

        _walletRepository.GetWalletByIdAsync(walletId, _userId, Arg.Any<CancellationToken>())
            .Returns(wallet);

        _transactionRepository.GetByIdAsync(transactionId)
            .Returns(transaction);

        _accountAccessService.CanAccessAccountAsync(_userId, accountId)
            .Returns(false);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("You don't have permission to access this transaction's account.");

        await _walletRepository.DidNotReceive().CreateAllocationAsync(Arg.Any<WalletAllocation>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenTransactionNotFound_ShouldThrowArgumentException()
    {
        // Arrange
        var walletId = 1;
        var transactionId = 999;

        var command = new CreateWalletAllocationCommand
        {
            WalletId = walletId,
            TransactionId = transactionId,
            Amount = 50m,
            UserId = _userId
        };

        var wallet = new Wallet
        {
            Id = walletId,
            Name = "Savings",
            UserId = _userId,
            Allocations = new List<WalletAllocation>()
        };

        _walletRepository.GetWalletByIdAsync(walletId, _userId, Arg.Any<CancellationToken>())
            .Returns(wallet);

        _transactionRepository.GetByIdAsync(transactionId)
            .Returns((Transaction?)null);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Transaction not found.");

        await _accountAccessService.DidNotReceive().CanAccessAccountAsync(Arg.Any<Guid>(), Arg.Any<int>());
        await _walletRepository.DidNotReceive().CreateAllocationAsync(Arg.Any<WalletAllocation>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithZeroAmount_ShouldThrowArgumentException()
    {
        // Arrange
        var walletId = 1;
        var transactionId = 10;
        var accountId = 5;

        var command = new CreateWalletAllocationCommand
        {
            WalletId = walletId,
            TransactionId = transactionId,
            Amount = 0m,
            UserId = _userId
        };

        var wallet = new Wallet
        {
            Id = walletId,
            Name = "Savings",
            UserId = _userId,
            Allocations = new List<WalletAllocation>()
        };

        var transaction = new Transaction
        {
            Id = transactionId,
            AccountId = accountId,
            Description = "Some transaction",
            Amount = 100m
        };

        _walletRepository.GetWalletByIdAsync(walletId, _userId, Arg.Any<CancellationToken>())
            .Returns(wallet);

        _transactionRepository.GetByIdAsync(transactionId)
            .Returns(transaction);

        _accountAccessService.CanAccessAccountAsync(_userId, accountId)
            .Returns(true);

        // Act
        Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Allocation amount cannot be zero.");

        await _walletRepository.DidNotReceive().CreateAllocationAsync(Arg.Any<WalletAllocation>(), Arg.Any<CancellationToken>());
    }
}
