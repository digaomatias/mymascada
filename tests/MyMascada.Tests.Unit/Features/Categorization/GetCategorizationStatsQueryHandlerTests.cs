using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Queries;

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
                ["Rule"] = 50,
                ["ML"] = 25,
                ["LLM"] = 10,
                ["BankCategory"] = 15
            });
        _transactionRepo.CountUncategorizedTransactionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(7);
        _ruleSuggestionRepo.CountPendingSuggestionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(3);

        var result = await _handler.Handle(new GetCategorizationStatsQuery { UserId = _userId }, CancellationToken.None);

        result.AutoCategorizedThisMonth.Should().Be(100);
        result.ProcessedByRules.Should().Be(50);
        result.ProcessedByML.Should().Be(25);
        result.ProcessedByLLM.Should().Be(10);
        result.ProcessedByBankCategory.Should().Be(15);
        result.RulesPercentage.Should().Be(50);
        result.MLPercentage.Should().Be(25);
        result.LLMPercentage.Should().Be(10);
        result.BankCategoryPercentage.Should().Be(15);
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
        _ruleSuggestionRepo.CountPendingSuggestionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(0);

        var result = await _handler.Handle(new GetCategorizationStatsQuery { UserId = _userId }, CancellationToken.None);

        result.AutoCategorizedThisMonth.Should().Be(0);
        result.RulesPercentage.Should().Be(0);
        result.MLPercentage.Should().Be(0);
        result.LLMPercentage.Should().Be(0);
        result.BankCategoryPercentage.Should().Be(0);
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
        _ruleSuggestionRepo.CountPendingSuggestionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(0);

        var result = await _handler.Handle(new GetCategorizationStatsQuery { UserId = _userId }, CancellationToken.None);

        result.ProcessedByRules.Should().Be(25);
    }

    [Fact]
    public async Task Handle_CountsBankCategoryInTotalAndPercentages()
    {
        // Regression: BankCategoryHandler tags applied transactions with
        // HandlerType "BankCategory". Without this case the dashboard would
        // drop bank-mapped auto-categorizations from the monthly totals.
        _transactionRepo.GetAutoCategorizationCountsByMethodAsync(
                _userId, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, int>
            {
                ["BankCategory"] = 40,
                ["Rule"] = 60
            });
        _transactionRepo.CountUncategorizedTransactionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(0);
        _ruleSuggestionRepo.CountPendingSuggestionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(0);

        var result = await _handler.Handle(new GetCategorizationStatsQuery { UserId = _userId }, CancellationToken.None);

        result.AutoCategorizedThisMonth.Should().Be(100);
        result.ProcessedByBankCategory.Should().Be(40);
        result.BankCategoryPercentage.Should().Be(40);
        result.ProcessedByRules.Should().Be(60);
        result.RulesPercentage.Should().Be(60);
    }

    [Fact]
    public async Task Handle_PercentagesAlwaysSumToExactly100_UsingLargestRemainder()
    {
        // Three equal buckets (1/1/1) would naively round to 33/33/33 = 99.
        // The largest-remainder (Hamilton) apportionment must allocate the
        // extra point so the row adds up to 100.
        _transactionRepo.GetAutoCategorizationCountsByMethodAsync(
                _userId, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, int>
            {
                ["Rule"] = 1,
                ["ML"] = 1,
                ["LLM"] = 1
            });
        _transactionRepo.CountUncategorizedTransactionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(0);
        _ruleSuggestionRepo.CountPendingSuggestionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(0);

        var result = await _handler.Handle(new GetCategorizationStatsQuery { UserId = _userId }, CancellationToken.None);

        result.AutoCategorizedThisMonth.Should().Be(3);
        var sum = result.RulesPercentage + result.MLPercentage
            + result.LLMPercentage + result.BankCategoryPercentage;
        sum.Should().Be(100);
    }

    [Fact]
    public async Task Handle_UsesCountPendingSuggestionsAsync_NotMaterializedQuery()
    {
        // Regression: PendingSuggestions used to call GetPendingSuggestionsAsync
        // which materializes the SuggestedCategory + SampleTransactions graph
        // just to get a Count(). The handler must use the dedicated count API.
        _transactionRepo.GetAutoCategorizationCountsByMethodAsync(
                _userId, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, int>());
        _transactionRepo.CountUncategorizedTransactionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(0);
        _ruleSuggestionRepo.CountPendingSuggestionsAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(42);

        var result = await _handler.Handle(new GetCategorizationStatsQuery { UserId = _userId }, CancellationToken.None);

        result.PendingSuggestions.Should().Be(42);
        await _ruleSuggestionRepo.Received(1).CountPendingSuggestionsAsync(
            _userId, Arg.Any<CancellationToken>());
        await _ruleSuggestionRepo.DidNotReceiveWithAnyArgs().GetPendingSuggestionsAsync(
            default!, default);
    }
}
