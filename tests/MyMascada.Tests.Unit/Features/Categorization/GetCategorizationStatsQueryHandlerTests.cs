using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Queries;
using MyMascada.Domain.Entities;

namespace MyMascada.Tests.Unit.Features.Categorization;

public class GetCategorizationStatsQueryHandlerTests
{
    private readonly ITransactionRepository _transactionRepo;
    private readonly IRuleSuggestionRepository _ruleSuggestionRepo;
    private readonly GetCategorizationStatsQueryHandler _handler;
    private readonly Guid _userId = Guid.NewGuid();

    public GetCategorizationStatsQueryHandlerTests()
    {
        _transactionRepo = Substitute.For<ITransactionRepository>();
        _ruleSuggestionRepo = Substitute.For<IRuleSuggestionRepository>();
        _handler = new GetCategorizationStatsQueryHandler(_transactionRepo, _ruleSuggestionRepo);
    }

    [Fact]
    public async Task Handle_ReturnsMonthlyCountsBrokenDownByMethod()
    {
        _transactionRepo.GetAutoCategorizationCountsByMethodAsync(
                _userId, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, int>
            {
                ["Rule"] = 60,
                ["ML"] = 30,
                ["LLM"] = 10
            });
        _transactionRepo.CountUncategorizedTransactionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(7);
        _ruleSuggestionRepo.GetPendingSuggestionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<RuleSuggestion> { new(), new(), new() });

        var result = await _handler.Handle(new GetCategorizationStatsQuery { UserId = _userId }, CancellationToken.None);

        result.AutoCategorizedThisMonth.Should().Be(100);
        result.ProcessedByRules.Should().Be(60);
        result.ProcessedByML.Should().Be(30);
        result.ProcessedByLLM.Should().Be(10);
        result.RulesPercentage.Should().Be(60);
        result.MLPercentage.Should().Be(30);
        result.LLMPercentage.Should().Be(10);
        result.NeedsReview.Should().Be(7);
        result.PendingSuggestions.Should().Be(3);
        result.PeriodEnd.Should().BeAfter(result.PeriodStart);
    }

    [Fact]
    public async Task Handle_ZeroAutoCategorized_ZeroPercentages()
    {
        _transactionRepo.GetAutoCategorizationCountsByMethodAsync(
                _userId, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, int>());
        _transactionRepo.CountUncategorizedTransactionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(0);
        _ruleSuggestionRepo.GetPendingSuggestionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<RuleSuggestion>());

        var result = await _handler.Handle(new GetCategorizationStatsQuery { UserId = _userId }, CancellationToken.None);

        result.AutoCategorizedThisMonth.Should().Be(0);
        result.RulesPercentage.Should().Be(0);
        result.MLPercentage.Should().Be(0);
        result.LLMPercentage.Should().Be(0);
        result.NeedsReview.Should().Be(0);
        result.PendingSuggestions.Should().Be(0);
    }

    [Fact]
    public async Task Handle_HandlesBothRuleAndRulesKeys()
    {
        // Historical data may use "Rule" or "Rules" — handler must sum both.
        _transactionRepo.GetAutoCategorizationCountsByMethodAsync(
                _userId, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, int>
            {
                ["Rule"] = 20,
                ["Rules"] = 5
            });
        _transactionRepo.CountUncategorizedTransactionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(0);
        _ruleSuggestionRepo.GetPendingSuggestionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<RuleSuggestion>());

        var result = await _handler.Handle(new GetCategorizationStatsQuery { UserId = _userId }, CancellationToken.None);

        result.ProcessedByRules.Should().Be(25);
    }
}
