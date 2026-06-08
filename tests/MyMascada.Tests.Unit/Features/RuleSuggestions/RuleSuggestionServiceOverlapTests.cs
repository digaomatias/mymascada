using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RuleSuggestions.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Tests.Unit.Features.RuleSuggestions;

/// <summary>
/// Covers the population-level overlap dedup in
/// <see cref="RuleSuggestionService.GenerateSuggestionsAsync"/>: a suggestion is
/// dropped when an existing active rule already catches >= 80% of the recent
/// transactions the suggestion would match, and a suggestion that matches no
/// recent transactions is dropped as useless. Drives the public API so the
/// real matcher (RulePatternMatcher) and the wiring are exercised together.
/// </summary>
public class RuleSuggestionServiceOverlapTests
{
    private readonly IRuleSuggestionRepository _ruleSuggestionRepo = Substitute.For<IRuleSuggestionRepository>();
    private readonly ITransactionRepository _transactionRepo = Substitute.For<ITransactionRepository>();
    private readonly ICategorizationRuleRepository _ruleRepo = Substitute.For<ICategorizationRuleRepository>();
    private readonly ICategoryRepository _categoryRepo = Substitute.For<ICategoryRepository>();
    private readonly IRuleSuggestionAnalyzerFactory _analyzerFactory = Substitute.For<IRuleSuggestionAnalyzerFactory>();
    private readonly IRuleSuggestionAnalyzer _analyzer = Substitute.For<IRuleSuggestionAnalyzer>();
    private readonly ICategorizationHistoryRepository _historyRepo = Substitute.For<ICategorizationHistoryRepository>();
    private readonly IFeatureFlags _featureFlags = Substitute.For<IFeatureFlags>();
    private readonly ISubscriptionService _subscriptionService = Substitute.For<ISubscriptionService>();
    private readonly RuleSuggestionService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public RuleSuggestionServiceOverlapTests()
    {
        _sut = new RuleSuggestionService(
            _ruleSuggestionRepo, _transactionRepo, _ruleRepo, _categoryRepo,
            _analyzerFactory, _historyRepo, _featureFlags, _subscriptionService);
    }

    // ---- helpers ---------------------------------------------------------

    private static Transaction Tx(int id, string description) => new()
    {
        Id = id,
        Description = description,
        Amount = -12.50m,
        TransactionDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static CategorizationRule ContainsRule(string pattern, int categoryId = 99) => new()
    {
        Pattern = pattern,
        Type = RuleType.Contains,
        IsActive = true,
        CategoryId = categoryId
    };

    private static PatternSuggestion ContainsSuggestion(string pattern, int categoryId = 5, double confidence = 0.9) => new()
    {
        Pattern = pattern,
        SuggestedCategoryId = categoryId,
        SuggestedCategoryName = "Gym",
        ConfidenceScore = confidence,
        SuggestedRuleType = RuleType.Contains,
        MatchingTransactions = new List<Transaction> { Tx(1, $"{pattern} SAMPLE") }
    };

    /// Pads to >= 10 transactions so GenerateSuggestionsAsync clears its
    /// "not enough data" guard, then appends the supplied population rows.
    private static List<Transaction> Population(params Transaction[] rows)
    {
        var padding = Enumerable.Range(100, 10)
            .Select(i => Tx(i, $"UNRELATED PADDING {i}"))
            .ToList();
        padding.AddRange(rows);
        return padding;
    }

    private void Arrange(List<Transaction> population, List<CategorizationRule> existingRules, List<PatternSuggestion> analyzerOutput)
    {
        _transactionRepo.GetRecentTransactionsAsync(_userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(population);
        _categoryRepo.GetByUserIdAsync(_userId).Returns(new List<Category>());
        _categoryRepo.GetSystemCategoriesAsync().Returns(new List<Category>());
        _ruleRepo.GetActiveRulesForUserAsync(_userId, Arg.Any<CancellationToken>()).Returns(existingRules);
        _subscriptionService.TryReserveRuleSuggestionQuotaAsync(_userId, Arg.Any<CancellationToken>()).Returns(false);
        _featureFlags.AiCategorization.Returns(false);
        _analyzer.AnalysisMethod.Returns("Basic Pattern Analysis");
        _analyzer.AnalyzePatternsAsync(Arg.Any<RuleAnalysisInput>(), Arg.Any<CancellationToken>()).Returns(analyzerOutput);
        _analyzerFactory.CreateAnalyzerAsync(Arg.Any<Guid>(), Arg.Any<RuleAnalysisConfiguration>()).Returns(_analyzer);
        _ruleSuggestionRepo.GetSimilarSuggestionsAsync(_userId, Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<RuleSuggestion>());
        _ruleSuggestionRepo.CreateSuggestionAsync(Arg.Any<RuleSuggestion>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<RuleSuggestion>());
    }

    // ---- tests -----------------------------------------------------------

    [Fact]
    public async Task Generate_SuggestionCoveredByExistingRule_IsDropped()
    {
        // 5 "JETTS" transactions already caught by an existing "JETT" rule. The
        // suggested "JETTS" pattern would match the same 5 → 100% covered → drop.
        var jetts = Enumerable.Range(1, 5).Select(i => Tx(i, $"JETTS LOWER HUTT {i}")).ToArray();
        Arrange(
            Population(jetts),
            new List<CategorizationRule> { ContainsRule("JETT") },
            new List<PatternSuggestion> { ContainsSuggestion("JETTS") });

        var result = await _sut.GenerateSuggestionsAsync(_userId);

        result.Should().BeEmpty();
        await _ruleSuggestionRepo.DidNotReceive()
            .CreateSuggestionAsync(Arg.Any<RuleSuggestion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_SuggestionNotCoveredByAnyRule_IsSaved()
    {
        // 5 "SPOTIFY" transactions, no existing rules → 0% covered → keep.
        var spotify = Enumerable.Range(1, 5).Select(i => Tx(i, $"SPOTIFY PREMIUM {i}")).ToArray();
        Arrange(
            Population(spotify),
            new List<CategorizationRule>(),
            new List<PatternSuggestion> { ContainsSuggestion("SPOTIFY") });

        var result = await _sut.GenerateSuggestionsAsync(_userId);

        result.Should().ContainSingle(s => s.Pattern == "SPOTIFY");
        await _ruleSuggestionRepo.Received(1)
            .CreateSuggestionAsync(Arg.Is<RuleSuggestion>(s => s.Pattern == "SPOTIFY"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_SuggestionMatchingNoRecentTransactions_IsDropped()
    {
        // The suggested "NETFLIX" pattern matches none of the recent transactions —
        // the analyzer can't have learned anything useful from it, so drop it.
        Arrange(
            Population(Tx(1, "SPOTIFY PREMIUM"), Tx(2, "JETTS GYM")),
            new List<CategorizationRule>(),
            new List<PatternSuggestion> { ContainsSuggestion("NETFLIX") });

        var result = await _sut.GenerateSuggestionsAsync(_userId);

        result.Should().BeEmpty();
        await _ruleSuggestionRepo.DidNotReceive()
            .CreateSuggestionAsync(Arg.Any<RuleSuggestion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_OverlapBelowThreshold_IsSaved()
    {
        // Suggested "COFFEE" matches 5 transactions; an existing "STARBUCKS" rule
        // covers only 2 of them → 40% < 80% threshold → keep the suggestion.
        var coffee = new[]
        {
            Tx(1, "COFFEE BEAN CO"),
            Tx(2, "COFFEE BEAN CO"),
            Tx(3, "COFFEE BEAN CO"),
            Tx(4, "STARBUCKS COFFEE"),
            Tx(5, "STARBUCKS COFFEE"),
        };
        Arrange(
            Population(coffee),
            new List<CategorizationRule> { ContainsRule("STARBUCKS") },
            new List<PatternSuggestion> { ContainsSuggestion("COFFEE") });

        var result = await _sut.GenerateSuggestionsAsync(_userId);

        result.Should().ContainSingle(s => s.Pattern == "COFFEE");
        await _ruleSuggestionRepo.Received(1)
            .CreateSuggestionAsync(Arg.Is<RuleSuggestion>(s => s.Pattern == "COFFEE"), Arg.Any<CancellationToken>());
    }
}
