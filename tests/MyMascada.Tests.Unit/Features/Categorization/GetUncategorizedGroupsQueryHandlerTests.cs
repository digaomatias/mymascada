using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Queries;
using MyMascada.Domain.Entities;

namespace MyMascada.Tests.Unit.Features.Categorization;

public class GetUncategorizedGroupsQueryHandlerTests
{
    private readonly ITransactionRepository _transactionRepo;
    private readonly GetUncategorizedGroupsQueryHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public GetUncategorizedGroupsQueryHandlerTests()
    {
        _transactionRepo = Substitute.For<ITransactionRepository>();
        _handler = new GetUncategorizedGroupsQueryHandler(_transactionRepo);
    }

    private static Transaction MakeTransaction(int id, string description, decimal amount)
        => new()
        {
            Id = id,
            Description = description,
            Amount = amount,
            TransactionDate = DateTime.UtcNow,
            AccountId = 1,
            Account = new Account { Id = 1, Name = "Main" }
        };

    [Fact]
    public async Task Handle_GroupsByNormalizedDescription_OrdersByFrequency()
    {
        // 3 netflix, 2 pak n save, 1 uber — normalized forms differ in original text
        var txns = new List<Transaction>
        {
            MakeTransaction(1, "NETFLIX.COM 10/03/2026", -14.99m),
            MakeTransaction(2, "NETFLIX.COM 10/02/2026", -14.99m),
            MakeTransaction(3, "NETFLIX.COM 10/01/2026 #REF-123", -14.99m),
            MakeTransaction(4, "PAK N SAVE PETONE", -120.50m),
            MakeTransaction(5, "PAK N SAVE PETONE", -80.25m),
            MakeTransaction(6, "UBER *EATS", -25.00m),
        };

        _transactionRepo.GetUncategorizedTransactionsAsync(_userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(txns);
        _transactionRepo.CountUncategorizedTransactionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(6);

        var result = await _handler.Handle(new GetUncategorizedGroupsQuery { UserId = _userId }, CancellationToken.None);

        result.Groups.Should().HaveCount(3);
        result.TotalUncategorized.Should().Be(6);
        result.GroupedTransactions.Should().Be(6);

        // First group is the most frequent
        result.Groups[0].TransactionCount.Should().Be(3);
        result.Groups[0].NormalizedDescription.Should().Contain("netflix");
        result.Groups[0].TransactionIds.Should().BeEquivalentTo(new[] { 1, 2, 3 });

        result.Groups[1].TransactionCount.Should().Be(2);
        result.Groups[2].TransactionCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_RespectsMinGroupSize()
    {
        var txns = new List<Transaction>
        {
            MakeTransaction(1, "NETFLIX.COM", -14.99m),
            MakeTransaction(2, "NETFLIX.COM", -14.99m),
            MakeTransaction(3, "UBER *EATS", -25.00m),
        };

        _transactionRepo.GetUncategorizedTransactionsAsync(_userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(txns);
        _transactionRepo.CountUncategorizedTransactionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(3);

        var result = await _handler.Handle(
            new GetUncategorizedGroupsQuery { UserId = _userId, MinGroupSize = 2 },
            CancellationToken.None);

        result.Groups.Should().HaveCount(1);
        result.Groups[0].TransactionCount.Should().Be(2);
        result.GroupedTransactions.Should().Be(2); // only the kept group counts
    }

    [Fact]
    public async Task Handle_NoTransactions_ReturnsEmptyResult()
    {
        _transactionRepo.GetUncategorizedTransactionsAsync(_userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<Transaction>());
        _transactionRepo.CountUncategorizedTransactionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(0);

        var result = await _handler.Handle(new GetUncategorizedGroupsQuery { UserId = _userId }, CancellationToken.None);

        result.Groups.Should().BeEmpty();
        result.TotalUncategorized.Should().Be(0);
        result.GroupedTransactions.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SkipsTransactionsWithEmptyNormalizedDescription()
    {
        var txns = new List<Transaction>
        {
            // Whitespace-only description normalizes to empty string and must be
            // dropped from the grouping — otherwise it would surface as a ghost
            // group in the wizard with no meaningful label.
            MakeTransaction(1, "   ", 10m),
            MakeTransaction(2, "NETFLIX.COM", -14.99m),
            MakeTransaction(3, "NETFLIX.COM", -14.99m),
        };

        _transactionRepo.GetUncategorizedTransactionsAsync(_userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(txns);
        _transactionRepo.CountUncategorizedTransactionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(3);

        var result = await _handler.Handle(new GetUncategorizedGroupsQuery { UserId = _userId }, CancellationToken.None);

        // Exactly one group — only netflix. The whitespace-only row is dropped by
        // the `IsNullOrWhiteSpace` filter in the handler.
        result.Groups.Should().HaveCount(1);
        result.Groups[0].NormalizedDescription.Should().Contain("netflix");
        result.Groups[0].TransactionCount.Should().Be(2);
        result.Groups[0].TransactionIds.Should().BeEquivalentTo(new[] { 2, 3 });
    }
}
