using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.Commands;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Tests.Unit.Commands;

public class UpdateTransactionSplitsCommandHandlerTests
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IAccountAccessService _accountAccessService;
    private readonly UpdateTransactionSplitsCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public UpdateTransactionSplitsCommandHandlerTests()
    {
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _categoryRepository = Substitute.For<ICategoryRepository>();
        _accountAccessService = Substitute.For<IAccountAccessService>();
        _handler = new UpdateTransactionSplitsCommandHandler(
            _transactionRepository, _categoryRepository, _accountAccessService);
    }

    private Transaction CreateTransaction(
        int id = 1,
        decimal amount = -100m,
        Guid? transferId = null,
        TransactionType type = TransactionType.Expense,
        List<TransactionSplit>? splits = null)
    {
        return new Transaction
        {
            Id = id,
            Amount = amount,
            TransactionDate = DateTime.UtcNow,
            Description = "Test transaction",
            AccountId = 10,
            Type = type,
            TransferId = transferId,
            Splits = splits ?? new List<TransactionSplit>(),
            Account = new Account { Id = 10, Name = "Checking", UserId = _userId }
        };
    }

    private void SetupHappyPath(Transaction transaction)
    {
        _transactionRepository.GetByIdWithSplitsAsync(transaction.Id, _userId).Returns(transaction);
        _accountAccessService.CanModifyAccountAsync(_userId, transaction.AccountId).Returns(true);
        _categoryRepository.ExistsAsync(Arg.Any<int>(), _userId).Returns(true);
    }

    private UpdateTransactionSplitsCommand CreateCommand(int transactionId, List<TransactionSplitInputDto>? splits)
    {
        return new UpdateTransactionSplitsCommand
        {
            UserId = _userId,
            TransactionId = transactionId,
            Splits = splits
        };
    }

    [Fact]
    public async Task Handle_WhenTransactionNotFound_ShouldThrowArgumentException()
    {
        _transactionRepository.GetByIdWithSplitsAsync(99, _userId).Returns((Transaction?)null);

        var command = CreateCommand(99, new List<TransactionSplitInputDto>
        {
            new() { CategoryId = 1, Amount = -100m }
        });

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not found or does not belong to user*");
        await _transactionRepository.DidNotReceive().UpdateAsync(Arg.Any<Transaction>());
    }

    [Fact]
    public async Task Handle_WhenUserCannotModifyAccount_ShouldThrowUnauthorizedAccessException()
    {
        var transaction = CreateTransaction();
        _transactionRepository.GetByIdWithSplitsAsync(transaction.Id, _userId).Returns(transaction);
        _accountAccessService.CanModifyAccountAsync(_userId, transaction.AccountId).Returns(false);

        var command = CreateCommand(transaction.Id, new List<TransactionSplitInputDto>
        {
            new() { CategoryId = 1, Amount = -100m }
        });

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _transactionRepository.DidNotReceive().UpdateAsync(Arg.Any<Transaction>());
    }

    [Fact]
    public async Task Handle_WhenTransactionIsTransfer_ShouldThrowArgumentException()
    {
        var transaction = CreateTransaction(transferId: Guid.NewGuid(), type: TransactionType.TransferComponent);
        SetupHappyPath(transaction);

        var command = CreateCommand(transaction.Id, new List<TransactionSplitInputDto>
        {
            new() { CategoryId = 1, Amount = -100m }
        });

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Transfer transactions cannot be split");
        await _transactionRepository.DidNotReceive().UpdateAsync(Arg.Any<Transaction>());
    }

    [Fact]
    public async Task Handle_WhenSumDoesNotMatchTransactionAmount_ShouldThrowArgumentException()
    {
        var transaction = CreateTransaction(amount: -100m);
        SetupHappyPath(transaction);

        var command = CreateCommand(transaction.Id, new List<TransactionSplitInputDto>
        {
            new() { CategoryId = 1, Amount = -60m },
            new() { CategoryId = 2, Amount = -30m } // sums to -90, not -100
        });

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*must sum to the transaction amount exactly*");
        await _transactionRepository.DidNotReceive().UpdateAsync(Arg.Any<Transaction>());
    }

    [Fact]
    public async Task Handle_WhenSplitHasOppositeSign_ShouldThrowArgumentException()
    {
        var transaction = CreateTransaction(amount: -100m);
        SetupHappyPath(transaction);

        var command = CreateCommand(transaction.Id, new List<TransactionSplitInputDto>
        {
            new() { CategoryId = 1, Amount = -150m },
            new() { CategoryId = 2, Amount = 50m } // positive split on an expense
        });

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*same sign as the transaction amount*");
        await _transactionRepository.DidNotReceive().UpdateAsync(Arg.Any<Transaction>());
    }

    [Fact]
    public async Task Handle_WhenSplitAmountIsZero_ShouldThrowArgumentException()
    {
        var transaction = CreateTransaction(amount: -100m);
        SetupHappyPath(transaction);

        var command = CreateCommand(transaction.Id, new List<TransactionSplitInputDto>
        {
            new() { CategoryId = 1, Amount = -100m },
            new() { CategoryId = 2, Amount = 0m }
        });

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*non-zero*");
        await _transactionRepository.DidNotReceive().UpdateAsync(Arg.Any<Transaction>());
    }

    [Fact]
    public async Task Handle_WhenTransactionAmountIsZero_ShouldThrowArgumentException()
    {
        var transaction = CreateTransaction(amount: 0m);
        SetupHappyPath(transaction);

        var command = CreateCommand(transaction.Id, new List<TransactionSplitInputDto>
        {
            new() { CategoryId = 1, Amount = -10m }
        });

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*zero-amount transaction cannot be split*");
        await _transactionRepository.DidNotReceive().UpdateAsync(Arg.Any<Transaction>());
    }

    [Fact]
    public async Task Handle_WhenCategoryDoesNotBelongToUser_ShouldThrowArgumentException()
    {
        var transaction = CreateTransaction(amount: -100m);
        _transactionRepository.GetByIdWithSplitsAsync(transaction.Id, _userId).Returns(transaction);
        _accountAccessService.CanModifyAccountAsync(_userId, transaction.AccountId).Returns(true);
        _categoryRepository.ExistsAsync(1, _userId).Returns(true);
        _categoryRepository.ExistsAsync(2, _userId).Returns(false);

        var command = CreateCommand(transaction.Id, new List<TransactionSplitInputDto>
        {
            new() { CategoryId = 1, Amount = -60m },
            new() { CategoryId = 2, Amount = -40m }
        });

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Category with ID 2 not found or does not belong to user");
        await _transactionRepository.DidNotReceive().UpdateAsync(Arg.Any<Transaction>());
    }

    [Fact]
    public async Task Handle_WithValidSplits_ShouldReplaceExistingSplitsAtomically()
    {
        var existingSplit = new TransactionSplit { Id = 5, TransactionId = 1, CategoryId = 9, Amount = -100m };
        var transaction = CreateTransaction(amount: -100m, splits: new List<TransactionSplit> { existingSplit });
        SetupHappyPath(transaction);

        var command = CreateCommand(transaction.Id, new List<TransactionSplitInputDto>
        {
            new() { CategoryId = 1, Amount = -60m, Description = "Groceries part" },
            new() { CategoryId = 2, Amount = -40m }
        });

        var result = await _handler.Handle(command, CancellationToken.None);

        // Old split is soft-deleted, not removed
        existingSplit.IsDeleted.Should().BeTrue();
        existingSplit.DeletedAt.Should().NotBeNull();

        // Two new active splits added
        var activeSplits = transaction.Splits.Where(s => !s.IsDeleted).ToList();
        activeSplits.Should().HaveCount(2);
        activeSplits.Select(s => s.CategoryId).Should().BeEquivalentTo(new[] { 1, 2 });
        activeSplits.Sum(s => s.Amount).Should().Be(-100m);
        activeSplits.Single(s => s.CategoryId == 1).Description.Should().Be("Groceries part");

        // Persisted exactly once (single SaveChanges -> atomic replace)
        await _transactionRepository.Received(1).UpdateAsync(transaction);

        // Response carries the new splits
        result.Splits.Should().NotBeNull();
        result.Splits.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithEmptyList_ShouldClearAllSplits()
    {
        var splitA = new TransactionSplit { Id = 5, TransactionId = 1, CategoryId = 1, Amount = -60m };
        var splitB = new TransactionSplit { Id = 6, TransactionId = 1, CategoryId = 2, Amount = -40m };
        var transaction = CreateTransaction(amount: -100m, splits: new List<TransactionSplit> { splitA, splitB });
        SetupHappyPath(transaction);

        var command = CreateCommand(transaction.Id, new List<TransactionSplitInputDto>());

        var result = await _handler.Handle(command, CancellationToken.None);

        splitA.IsDeleted.Should().BeTrue();
        splitB.IsDeleted.Should().BeTrue();
        transaction.Splits.Where(s => !s.IsDeleted).Should().BeEmpty();
        await _transactionRepository.Received(1).UpdateAsync(transaction);
        result.Splits.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithNullList_ShouldClearAllSplits()
    {
        var existingSplit = new TransactionSplit { Id = 5, TransactionId = 1, CategoryId = 1, Amount = -100m };
        var transaction = CreateTransaction(amount: -100m, splits: new List<TransactionSplit> { existingSplit });
        SetupHappyPath(transaction);

        var command = CreateCommand(transaction.Id, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        existingSplit.IsDeleted.Should().BeTrue();
        await _transactionRepository.Received(1).UpdateAsync(transaction);
        result.Splits.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithPositiveIncomeTransaction_ShouldAcceptPositiveSplits()
    {
        var transaction = CreateTransaction(amount: 250m, type: TransactionType.Income);
        SetupHappyPath(transaction);

        var command = CreateCommand(transaction.Id, new List<TransactionSplitInputDto>
        {
            new() { CategoryId = 1, Amount = 200m },
            new() { CategoryId = 2, Amount = 50m }
        });

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Splits.Should().HaveCount(2);
        transaction.Splits.Where(s => !s.IsDeleted).Sum(s => s.Amount).Should().Be(250m);
    }

    [Fact]
    public async Task Handle_ShouldNotChangeParentTransactionCategory()
    {
        var transaction = CreateTransaction(amount: -100m);
        transaction.CategoryId = 42;
        SetupHappyPath(transaction);

        var command = CreateCommand(transaction.Id, new List<TransactionSplitInputDto>
        {
            new() { CategoryId = 1, Amount = -60m },
            new() { CategoryId = 2, Amount = -40m }
        });

        await _handler.Handle(command, CancellationToken.None);

        transaction.CategoryId.Should().Be(42);
    }
}
