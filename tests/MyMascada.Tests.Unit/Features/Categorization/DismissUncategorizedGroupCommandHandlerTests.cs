using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Commands;
using MyMascada.Domain.Entities;

namespace MyMascada.Tests.Unit.Features.Categorization;

public class DismissUncategorizedGroupCommandHandlerTests
{
    private readonly ITransactionRepository _transactionRepo;
    private readonly DismissUncategorizedGroupCommandHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public DismissUncategorizedGroupCommandHandlerTests()
    {
        _transactionRepo = Substitute.For<ITransactionRepository>();
        _handler = new DismissUncategorizedGroupCommandHandler(
            _transactionRepo,
            Substitute.For<ILogger<DismissUncategorizedGroupCommandHandler>>());
    }

    [Fact]
    public async Task Handle_EmptyTransactionIds_ReturnsFailure()
    {
        var result = await _handler.Handle(
            new DismissUncategorizedGroupCommand { UserId = _userId },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.TransactionsUpdated.Should().Be(0);
        await _transactionRepo.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_TooManyIds_ReturnsFailureWithoutTouchingRepo()
    {
        var command = new DismissUncategorizedGroupCommand
        {
            UserId = _userId,
            TransactionIds = Enumerable.Range(1, 501).ToList()
        };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("500");
        // Guard rejects before any data access.
        await _transactionRepo.DidNotReceive().GetTransactionsByIdsAsync(
            Arg.Any<IEnumerable<int>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _transactionRepo.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_NoMatchingTransactions_ReturnsFailure()
    {
        // GetTransactionsByIdsAsync filters to the user's own rows, so a
        // crafted request for someone else's ids comes back empty — the
        // handler must report failure and never persist.
        _transactionRepo.GetTransactionsByIdsAsync(
            Arg.Any<IEnumerable<int>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(new List<Transaction>());

        var result = await _handler.Handle(
            new DismissUncategorizedGroupCommand { UserId = _userId, TransactionIds = new() { 1, 2 } },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
        await _transactionRepo.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_SnoozeMode_SetsFutureSnoozeAndLeavesHiddenFlagFalse()
    {
        var transactions = new List<Transaction>
        {
            new() { Id = 1, AccountId = 10, Description = "NETFLIX.COM", CategoryId = null },
            new() { Id = 2, AccountId = 10, Description = "NETFLIX.COM", CategoryId = null }
        };
        _transactionRepo.GetTransactionsByIdsAsync(
            Arg.Any<IEnumerable<int>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(transactions);

        var before = DateTime.UtcNow;
        var result = await _handler.Handle(
            new DismissUncategorizedGroupCommand
            {
                UserId = _userId,
                TransactionIds = new() { 1, 2 },
                Mode = DismissUncategorizedGroupMode.Snooze,
                SnoozeDays = 30
            },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TransactionsUpdated.Should().Be(2);

        foreach (var t in transactions)
        {
            t.IsHiddenFromQuickCategorize.Should().BeFalse();
            t.QuickCategorizeSnoozedUntil.Should().NotBeNull();
            // ~30 days out (allow a generous window for clock drift in CI).
            t.QuickCategorizeSnoozedUntil!.Value
                .Should().BeCloseTo(before.AddDays(30), TimeSpan.FromMinutes(1));
            t.UpdatedBy.Should().Be(_userId.ToString());
        }

        await _transactionRepo.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_IgnoreMode_HidesPermanentlyAndClearsSnooze()
    {
        var transactions = new List<Transaction>
        {
            // A row that was previously snoozed — Ignore must clear the snooze
            // so the two flags can't disagree.
            new() { Id = 1, AccountId = 10, Description = "SPOTIFY", QuickCategorizeSnoozedUntil = DateTime.UtcNow.AddDays(5) }
        };
        _transactionRepo.GetTransactionsByIdsAsync(
            Arg.Any<IEnumerable<int>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(transactions);

        var result = await _handler.Handle(
            new DismissUncategorizedGroupCommand
            {
                UserId = _userId,
                TransactionIds = new() { 1 },
                Mode = DismissUncategorizedGroupMode.Ignore
            },
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.TransactionsUpdated.Should().Be(1);
        transactions[0].IsHiddenFromQuickCategorize.Should().BeTrue();
        transactions[0].QuickCategorizeSnoozedUntil.Should().BeNull();

        await _transactionRepo.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_DoesNotTouchCategoryOrFinancialFields()
    {
        // The dismiss action is strictly a wizard-visibility change. Guard
        // against regressions that accidentally categorize or re-sign rows.
        var transaction = new Transaction
        {
            Id = 1,
            AccountId = 10,
            Description = "NETFLIX.COM",
            CategoryId = null,
            Amount = -42.50m,
            IsReviewed = false
        };
        _transactionRepo.GetTransactionsByIdsAsync(
            Arg.Any<IEnumerable<int>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(new List<Transaction> { transaction });

        await _handler.Handle(
            new DismissUncategorizedGroupCommand
            {
                UserId = _userId,
                TransactionIds = new() { 1 },
                Mode = DismissUncategorizedGroupMode.Ignore
            },
            CancellationToken.None);

        transaction.CategoryId.Should().BeNull();
        transaction.Amount.Should().Be(-42.50m);
        transaction.IsReviewed.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SnoozeDaysBelowOne_ClampsToAtLeastOneDay()
    {
        // SnoozeDays of 0 (or negative) must not produce a snooze that's
        // already expired — otherwise the group would reappear immediately.
        var transaction = new Transaction { Id = 1, AccountId = 10, Description = "X" };
        _transactionRepo.GetTransactionsByIdsAsync(
            Arg.Any<IEnumerable<int>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(new List<Transaction> { transaction });

        var before = DateTime.UtcNow;
        await _handler.Handle(
            new DismissUncategorizedGroupCommand
            {
                UserId = _userId,
                TransactionIds = new() { 1 },
                Mode = DismissUncategorizedGroupMode.Snooze,
                SnoozeDays = 0
            },
            CancellationToken.None);

        transaction.QuickCategorizeSnoozedUntil.Should().NotBeNull();
        transaction.QuickCategorizeSnoozedUntil!.Value.Should().BeAfter(before);
    }
}
