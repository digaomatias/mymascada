using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Commands;
using MyMascada.Application.Features.Categorization.Services;
using MyMascada.Domain.Entities;

namespace MyMascada.Tests.Unit.Features.Categorization;

public class BulkCategorizeGroupCommandHandlerTests
{
    private readonly ITransactionRepository _transactionRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IAccountAccessService _accountAccess;
    private readonly ICategorizationHistoryService _historyService;
    private readonly BulkCategorizeGroupCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public BulkCategorizeGroupCommandHandlerTests()
    {
        _transactionRepo = Substitute.For<ITransactionRepository>();
        _categoryRepo = Substitute.For<ICategoryRepository>();
        _accountAccess = Substitute.For<IAccountAccessService>();
        _historyService = Substitute.For<ICategorizationHistoryService>();
        _handler = new BulkCategorizeGroupCommandHandler(
            _transactionRepo,
            _categoryRepo,
            _accountAccess,
            _historyService,
            Substitute.For<ILogger<BulkCategorizeGroupCommandHandler>>());
    }

    [Fact]
    public async Task Handle_EmptyTransactionIds_ReturnsFailure()
    {
        var result = await _handler.Handle(
            new BulkCategorizeGroupCommand { UserId = _userId, CategoryId = 1 },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.TransactionsUpdated.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CategoryDoesNotExist_ReturnsFailure()
    {
        _categoryRepo.ExistsAsync(Arg.Any<int>(), _userId).Returns(false);

        var result = await _handler.Handle(
            new BulkCategorizeGroupCommand { UserId = _userId, CategoryId = 99, TransactionIds = new() { 1 } },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Category");
    }

    [Fact]
    public async Task Handle_ValidRequest_CategorizesAndRecordsHistory()
    {
        var transactions = new List<Transaction>
        {
            new() { Id = 1, AccountId = 10, Description = "NETFLIX.COM", CategoryId = null },
            new() { Id = 2, AccountId = 10, Description = "NETFLIX.COM FEB", CategoryId = null }
        };

        _categoryRepo.ExistsAsync(5, _userId).Returns(true);
        _transactionRepo.GetTransactionsByIdsAsync(
            Arg.Any<IEnumerable<int>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(transactions);
        _accountAccess.CanModifyAccountAsync(_userId, 10).Returns(true);

        var command = new BulkCategorizeGroupCommand
        {
            UserId = _userId,
            CategoryId = 5,
            TransactionIds = new() { 1, 2 },
            NormalizedDescription = "netflix com"
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TransactionsUpdated.Should().Be(2);
        transactions[0].CategoryId.Should().Be(5);
        transactions[1].CategoryId.Should().Be(5);
        transactions[0].IsReviewed.Should().BeTrue();
        transactions[1].IsReviewed.Should().BeTrue();

        await _transactionRepo.Received(1).SaveChangesAsync();
        // History recorded for each transaction that changed category
        await _historyService.Received(1).RecordCategorizationBatchAsync(
            Arg.Is<IEnumerable<CategorizationHistoryEvent>>(
                events => events.Count() == 2 && events.All(e => e.CategoryId == 5)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SkipsTransactionsAlreadyPartOfTransfer()
    {
        var transactions = new List<Transaction>
        {
            new() { Id = 1, AccountId = 10, Description = "OK", TransferId = null },
            new() { Id = 2, AccountId = 10, Description = "TRANSFER", TransferId = Guid.NewGuid() }
        };

        _categoryRepo.ExistsAsync(5, _userId).Returns(true);
        _transactionRepo.GetTransactionsByIdsAsync(
            Arg.Any<IEnumerable<int>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(transactions);
        _accountAccess.CanModifyAccountAsync(_userId, 10).Returns(true);

        var command = new BulkCategorizeGroupCommand
        {
            UserId = _userId,
            CategoryId = 5,
            TransactionIds = new() { 1, 2 }
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.TransactionsUpdated.Should().Be(1);
        result.Errors.Should().HaveCount(1);
        transactions[0].CategoryId.Should().Be(5);
        transactions[1].CategoryId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NoAccessToAccount_Throws()
    {
        var transactions = new List<Transaction>
        {
            new() { Id = 1, AccountId = 99, Description = "X" }
        };

        _categoryRepo.ExistsAsync(5, _userId).Returns(true);
        _transactionRepo.GetTransactionsByIdsAsync(
            Arg.Any<IEnumerable<int>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(transactions);
        _accountAccess.CanModifyAccountAsync(_userId, 99).Returns(false);

        var command = new BulkCategorizeGroupCommand
        {
            UserId = _userId,
            CategoryId = 5,
            TransactionIds = new() { 1 }
        };

        await FluentActions
            .Invoking(() => _handler.Handle(command, CancellationToken.None))
            .Should()
            .ThrowAsync<UnauthorizedAccessException>();
    }
}
