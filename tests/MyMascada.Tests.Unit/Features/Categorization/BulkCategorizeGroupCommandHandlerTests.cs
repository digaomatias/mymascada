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
    public async Task Handle_HomogeneousGroup_RecordsSingleAggregatedHistoryEntry()
    {
        // Every transaction in the group normalizes to the same key — the
        // handler should aggregate them into a single history entry so the
        // ML handler gets one strong signal for the user's confirmation.
        var transactions = new List<Transaction>
        {
            new() { Id = 1, AccountId = 10, Description = "NETFLIX.COM", CategoryId = null },
            new() { Id = 2, AccountId = 10, Description = "NETFLIX.COM", CategoryId = null }
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

        result.Success.Should().BeTrue();
        result.TransactionsUpdated.Should().Be(2);
        transactions[0].CategoryId.Should().Be(5);
        transactions[1].CategoryId.Should().Be(5);
        transactions[0].IsReviewed.Should().BeTrue();
        transactions[1].IsReviewed.Should().BeTrue();

        await _transactionRepo.Received(1).SaveChangesAsync();
        // Server-derived normalized key — not any client-supplied value — is
        // used for the aggregated entry.
        await _historyService.Received(1).RecordCategorizationBatchAsync(
            Arg.Is<IEnumerable<CategorizationHistoryEvent>>(
                events => events.Count() == 1
                    && events.Single().CategoryId == 5
                    && events.Single().Description == "netflix com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_HeterogeneousGroup_SkipsHistoryRecording()
    {
        // Transactions normalize to different keys — the quick-categorize
        // wizard only ever sends homogeneous groups, so hitting this branch
        // means either a bug upstream or a crafted request. Skip history
        // recording entirely rather than inflating N raw-description events
        // the user never confirmed as a coherent group.
        var transactions = new List<Transaction>
        {
            new() { Id = 1, AccountId = 10, Description = "NETFLIX.COM", CategoryId = null },
            new() { Id = 2, AccountId = 10, Description = "SPOTIFY PREMIUM", CategoryId = null }
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

        // Categorization still applied...
        result.Success.Should().BeTrue();
        result.TransactionsUpdated.Should().Be(2);
        transactions[0].CategoryId.Should().Be(5);
        transactions[1].CategoryId.Should().Be(5);

        // ...but no history entries written, so ML signals don't get
        // inflated by a heterogeneous batch.
        await _historyService.DidNotReceive().RecordCategorizationBatchAsync(
            Arg.Any<IEnumerable<CategorizationHistoryEvent>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RecordHistoryFalse_SkipsHistoryRecording()
    {
        // Subsequent chunks of a chunked group request opt out of history
        // recording so the same user action doesn't increment MatchCount
        // once per chunk.
        var transactions = new List<Transaction>
        {
            new() { Id = 1, AccountId = 10, Description = "NETFLIX.COM", CategoryId = null },
            new() { Id = 2, AccountId = 10, Description = "NETFLIX.COM", CategoryId = null }
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
            RecordHistory = false
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TransactionsUpdated.Should().Be(2);

        // Categorization still applied, but no history entries written.
        await _historyService.DidNotReceive().RecordCategorizationBatchAsync(
            Arg.Any<IEnumerable<CategorizationHistoryEvent>>(),
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
        result.UpdatedTransactionIds.Should().BeEquivalentTo(new[] { 1 });
        result.Errors.Should().HaveCount(1);
        transactions[0].CategoryId.Should().Be(5);
        transactions[1].CategoryId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AlreadyInTargetCategory_DoesNotCountOrTouchRows()
    {
        // Regression: submitting a group where some rows already have the
        // target category inflated TransactionsUpdated, dirtied EF entities
        // with redundant UPDATEs, and returned every id in
        // UpdatedTransactionIds — causing the wizard toast to lie
        // ("Categorized N transactions" when the real delta was smaller) and
        // leaving already-committed ids in the id-narrowing set.
        var alreadyCategorized = new Transaction
        {
            Id = 1,
            AccountId = 10,
            Description = "NETFLIX.COM",
            CategoryId = 5,
            IsReviewed = true,
            // UpdatedBy stays null to assert the handler doesn't touch it.
            UpdatedBy = null,
        };
        var uncategorized = new Transaction
        {
            Id = 2,
            AccountId = 10,
            Description = "NETFLIX.COM",
            CategoryId = null,
            IsReviewed = false,
            UpdatedBy = null,
        };
        var transactions = new List<Transaction> { alreadyCategorized, uncategorized };

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

        result.Success.Should().BeTrue();
        // Only the previously-uncategorized row counts — the already-
        // categorized row was a no-op.
        result.TransactionsUpdated.Should().Be(1);
        result.UpdatedTransactionIds.Should().BeEquivalentTo(new[] { 2 });

        // Already-categorized row must be left untouched so EF doesn't emit
        // a redundant UPDATE for it. UpdatedBy is `string?` (and starts null
        // in this test), so it's a reliable witness that the handler skipped
        // writing to the row. UpdatedAt is `DateTime` (non-nullable) and
        // auto-initializes in the base entity, so it's not checkable here.
        alreadyCategorized.UpdatedBy.Should().BeNull();
        alreadyCategorized.IsReviewed.Should().BeTrue();

        // The uncategorized row is updated as expected.
        uncategorized.CategoryId.Should().Be(5);
        uncategorized.IsReviewed.Should().BeTrue();
        uncategorized.UpdatedBy.Should().Be(_userId.ToString());
    }

    [Fact]
    public async Task Handle_AllRowsAlreadyInTargetCategory_RecordsNoHistoryAndReturnsZero()
    {
        // Re-submitting a group that's already fully categorized to the
        // target category is a no-op — no rows change, no history recorded.
        var transactions = new List<Transaction>
        {
            new() { Id = 1, AccountId = 10, Description = "NETFLIX.COM", CategoryId = 5 },
            new() { Id = 2, AccountId = 10, Description = "NETFLIX.COM", CategoryId = 5 }
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

        result.Success.Should().BeTrue();
        result.TransactionsUpdated.Should().Be(0);
        result.UpdatedTransactionIds.Should().BeEmpty();

        // No history recorded — the gate is `changedTransactions.Count > 0`.
        await _historyService.DidNotReceive().RecordCategorizationBatchAsync(
            Arg.Any<IEnumerable<CategorizationHistoryEvent>>(),
            Arg.Any<CancellationToken>());
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
