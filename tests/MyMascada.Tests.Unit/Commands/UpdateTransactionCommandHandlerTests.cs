using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Services;
using MyMascada.Application.Features.Transactions.Commands;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Tests.Unit.Commands;

public class UpdateTransactionCommandHandlerTests
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ITransferRepository _transferRepository;
    private readonly IAccountAccessService _accountAccessService;
    private readonly ICategorizationHistoryService _historyService;
    private readonly UpdateTransactionCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public UpdateTransactionCommandHandlerTests()
    {
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _categoryRepository = Substitute.For<ICategoryRepository>();
        _transferRepository = Substitute.For<ITransferRepository>();
        _accountAccessService = Substitute.For<IAccountAccessService>();
        _historyService = Substitute.For<ICategorizationHistoryService>();
        _handler = new UpdateTransactionCommandHandler(
            _transactionRepository,
            _categoryRepository,
            _transferRepository,
            _accountAccessService,
            _historyService);
    }

    private Transaction CreateSplitTransaction(decimal amount = -100m)
    {
        var transaction = new Transaction
        {
            Id = 1,
            Amount = amount,
            TransactionDate = DateTime.UtcNow,
            Description = "Supermarket",
            Status = TransactionStatus.Cleared,
            AccountId = 10,
            Account = new Account { Id = 10, Name = "Checking", UserId = _userId },
            Splits = new List<TransactionSplit>
            {
                new() { Id = 1, TransactionId = 1, CategoryId = 5, Amount = -60m },
                new() { Id = 2, TransactionId = 1, CategoryId = 6, Amount = -40m }
            }
        };

        _transactionRepository.GetByIdWithSplitsAsync(transaction.Id, _userId).Returns(transaction);
        _accountAccessService.CanModifyAccountAsync(_userId, transaction.AccountId).Returns(true);
        return transaction;
    }

    private UpdateTransactionCommand CreateCommand(Transaction transaction, decimal amount)
    {
        return new UpdateTransactionCommand
        {
            UserId = _userId,
            Id = transaction.Id,
            Amount = amount,
            TransactionDate = transaction.TransactionDate,
            Description = transaction.Description,
            Status = transaction.Status,
            CategoryId = null
        };
    }

    [Fact]
    public async Task Handle_WhenAmountChangesOnSplitTransaction_ShouldClearSplits()
    {
        var transaction = CreateSplitTransaction(amount: -100m);
        var command = CreateCommand(transaction, amount: -120m);

        await _handler.Handle(command, CancellationToken.None);

        // Splits would no longer sum to the new amount, so they must be cleared
        // (soft-deleted, same pattern as the splits replace endpoint).
        transaction.Splits.Should().AllSatisfy(s =>
        {
            s.IsDeleted.Should().BeTrue();
            s.DeletedAt.Should().NotBeNull();
        });
        transaction.Amount.Should().Be(-120m);
        await _transactionRepository.Received(1).UpdateAsync(transaction);
    }

    [Fact]
    public async Task Handle_WhenAmountChangesBySubCentOnSplitTransaction_ShouldClearSplits()
    {
        // Even a sub-cent change breaks the exact-sum invariant.
        var transaction = CreateSplitTransaction(amount: -100m);
        var command = CreateCommand(transaction, amount: -100.001m);

        await _handler.Handle(command, CancellationToken.None);

        transaction.Splits.Should().AllSatisfy(s => s.IsDeleted.Should().BeTrue());
    }

    [Fact]
    public async Task Handle_WhenAmountUnchangedOnSplitTransaction_ShouldPreserveSplits()
    {
        var transaction = CreateSplitTransaction(amount: -100m);
        var command = CreateCommand(transaction, amount: -100m);
        command.Notes = "Edited notes only";

        await _handler.Handle(command, CancellationToken.None);

        transaction.Splits.Should().AllSatisfy(s =>
        {
            s.IsDeleted.Should().BeFalse();
            s.DeletedAt.Should().BeNull();
        });
        transaction.Notes.Should().Be("Edited notes only");
        await _transactionRepository.Received(1).UpdateAsync(transaction);
    }
}
