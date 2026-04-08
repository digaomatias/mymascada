using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Features.Categorization.Handlers;
using MyMascada.Application.Features.Categorization.Services;
using MyMascada.Domain.Entities;

namespace MyMascada.Tests.Unit.Handlers;

public class MLHandlerTests
{
    private readonly ISimilarityMatchingService _similarityService;
    private readonly MLHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly CategorizationOptions _options;

    public MLHandlerTests()
    {
        _similarityService = Substitute.For<ISimilarityMatchingService>();
        _options = new CategorizationOptions { MLAutoApplyThreshold = 0.95m };
        var optionsMock = Substitute.For<IOptions<CategorizationOptions>>();
        optionsMock.Value.Returns(_options);

        _handler = new MLHandler(
            _similarityService,
            optionsMock,
            Substitute.For<ILogger<MLHandler>>());
    }

    [Fact]
    public async Task HandleAsync_NoTransactions_ReturnsEmpty()
    {
        var result = await _handler.HandleAsync(new List<Transaction>());

        result.Should().NotBeNull();
        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_HighConfidenceMatch_AutoApplies()
    {
        var transaction = CreateTransaction("NETFLIX.COM");
        var match = new SimilarityMatch(5, "Entertainment", 0.95m, "Exact", "netflix com");

        _similarityService.FindBestMatchAsync(_userId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(match);

        var result = await _handler.HandleAsync(new[] { transaction });

        result.AutoAppliedTransactions.Should().HaveCount(1);
        result.AutoAppliedTransactions[0].CategoryId.Should().Be(5);
        result.AutoAppliedTransactions[0].ConfidenceScore.Should().Be(0.95m);
        result.Candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_MediumConfidenceMatch_CreateCandidate()
    {
        var transaction = CreateTransaction("NEW MERCHANT");
        var match = new SimilarityMatch(10, "Groceries", 0.75m, "Fuzzy", "old merchant");

        _similarityService.FindBestMatchAsync(_userId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(match);

        var result = await _handler.HandleAsync(new[] { transaction });

        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().HaveCount(1);
        result.Candidates[0].CategoryId.Should().Be(10);
        result.Candidates[0].ConfidenceScore.Should().Be(0.75m);
        result.Candidates[0].CategorizationMethod.Should().Be(CandidateMethod.ML);
    }

    [Fact]
    public async Task HandleAsync_NoMatch_PassesThrough()
    {
        var transaction = CreateTransaction("UNKNOWN");
        _similarityService.FindBestMatchAsync(_userId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((SimilarityMatch?)null);

        var result = await _handler.HandleAsync(new[] { transaction });

        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().BeEmpty();
        result.CategorizedTransactions.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_NoUserId_ReturnsEmpty()
    {
        var transaction = new Transaction
        {
            Id = 1,
            Description = "NETFLIX.COM",
            Account = null! // No account means no userId
        };

        var result = await _handler.HandleAsync(new[] { transaction });

        result.AutoAppliedTransactions.Should().BeEmpty();
        result.Candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_MultipleTx_MixedResults()
    {
        var tx1 = CreateTransaction("NETFLIX.COM", 1);
        var tx2 = CreateTransaction("NEW STORE", 2);
        var tx3 = CreateTransaction("UNKNOWN", 3);

        _similarityService.FindBestMatchAsync(_userId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                new SimilarityMatch(5, "Entertainment", 0.95m, "Exact", "netflix com"),
                new SimilarityMatch(10, "Groceries", 0.70m, "Fuzzy", "old store"),
                (SimilarityMatch?)null);

        var result = await _handler.HandleAsync(new[] { tx1, tx2, tx3 });

        result.AutoAppliedTransactions.Should().HaveCount(1);
        result.Candidates.Should().HaveCount(1);
        result.Metrics.ProcessedByML.Should().Be(2);
    }

    private Transaction CreateTransaction(string description, int id = 1)
    {
        return new Transaction
        {
            Id = id,
            Description = description,
            Amount = -10.00m,
            Account = new Account
            {
                Id = 1,
                UserId = _userId,
                Name = "Test Account"
            }
        };
    }
}
