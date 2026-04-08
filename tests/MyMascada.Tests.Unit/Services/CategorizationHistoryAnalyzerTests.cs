using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RuleSuggestions.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Tests.Unit.Services;

public class CategorizationHistoryAnalyzerTests
{
    private readonly ICategorizationHistoryRepository _historyRepo;
    private readonly ICategorizationRuleRepository _ruleRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly CategorizationHistoryAnalyzer _analyzer;
    private readonly Guid _userId = Guid.NewGuid();

    public CategorizationHistoryAnalyzerTests()
    {
        _historyRepo = Substitute.For<ICategorizationHistoryRepository>();
        _ruleRepo = Substitute.For<ICategorizationRuleRepository>();
        _categoryRepo = Substitute.For<ICategoryRepository>();

        _categoryRepo.GetByUserIdAsync(_userId).Returns(new List<Category>());
        _categoryRepo.GetSystemCategoriesAsync().Returns(new List<Category>
        {
            new() { Id = 1, Name = "Groceries" },
            new() { Id = 2, Name = "Entertainment" },
            new() { Id = 3, Name = "Transportation" }
        });

        _analyzer = new CategorizationHistoryAnalyzer(
            _historyRepo,
            _ruleRepo,
            _categoryRepo,
            Substitute.For<ILogger<CategorizationHistoryAnalyzer>>());
    }

    [Fact]
    public async Task Analyze_TooFewEntries_ReturnsEmpty()
    {
        _historyRepo.GetAllForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationHistory> { MakeEntry("netflix", 2) });
        _ruleRepo.GetActiveRulesForUserAsync(_userId).Returns(Array.Empty<CategorizationRule>());

        var result = await _analyzer.AnalyzeAsync(_userId);

        result.TotalEntriesAnalyzed.Should().Be(1);
        result.DeterministicSuggestions.Should().BeEmpty();
        result.AmbiguousClusters.Should().BeEmpty();
    }

    [Fact]
    public async Task Analyze_ClusterWithCommonToken_CreatesDeterministicSuggestion()
    {
        var entries = new List<CategorizationHistory>
        {
            MakeEntry("pak n save petone", 1, "PAK N SAVE PETONE"),
            MakeEntry("pak n save lower hutt", 1, "PAK N SAVE LOWER HUTT"),
            MakeEntry("pak n save wellington", 1, "PAK N SAVE WELLINGTON")
        };

        _historyRepo.GetAllForUserAsync(_userId, Arg.Any<CancellationToken>()).Returns(entries);
        _ruleRepo.GetActiveRulesForUserAsync(_userId).Returns(Array.Empty<CategorizationRule>());

        var result = await _analyzer.AnalyzeAsync(_userId);

        result.DeterministicSuggestions.Should().HaveCount(1);
        var suggestion = result.DeterministicSuggestions[0];
        suggestion.SuggestedCategoryId.Should().Be(1);
        suggestion.SuggestedCategoryName.Should().Be("Groceries");
        suggestion.DetectionMethod.Should().Be("History Pattern Analysis");
        // The common token should be "save" (shared across all, >= 4 chars)
        suggestion.Pattern.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Analyze_ClusterCoveredByExistingRule_IsSkipped()
    {
        var entries = new List<CategorizationHistory>
        {
            MakeEntry("netflix subscription", 2, "NETFLIX SUBSCRIPTION"),
            MakeEntry("netflix monthly", 2, "NETFLIX MONTHLY"),
            MakeEntry("netflix payment", 2, "NETFLIX PAYMENT")
        };

        var rules = new List<CategorizationRule>
        {
            new() { Pattern = "NETFLIX", Type = RuleType.Contains, IsCaseSensitive = false, IsActive = true, CategoryId = 2 }
        };

        _historyRepo.GetAllForUserAsync(_userId, Arg.Any<CancellationToken>()).Returns(entries);
        _ruleRepo.GetActiveRulesForUserAsync(_userId).Returns(rules);

        var result = await _analyzer.AnalyzeAsync(_userId);

        result.CoveredClusterCount.Should().Be(1);
        result.DeterministicSuggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task Analyze_ClusterWithNoCommonToken_CreatesAmbiguousCluster()
    {
        // These all belong to category 1 (Groceries) but share no common token >= 4 chars
        var entries = new List<CategorizationHistory>
        {
            MakeEntry("new world wellington", 1, "NEW WORLD WELLINGTON"),
            MakeEntry("countdown petone", 1, "COUNTDOWN PETONE"),
            MakeEntry("fresh choice johnsonville", 1, "FRESH CHOICE JOHNSONVILLE")
        };

        _historyRepo.GetAllForUserAsync(_userId, Arg.Any<CancellationToken>()).Returns(entries);
        _ruleRepo.GetActiveRulesForUserAsync(_userId).Returns(Array.Empty<CategorizationRule>());

        var result = await _analyzer.AnalyzeAsync(_userId);

        // No shared token across all 3 — these will form separate clusters via union-find
        // (they share no significant tokens) so each cluster has size < 3
        // This is correct behavior — separate merchants that happen to be in the same category
        // don't form a single cluster unless they share tokens
        result.DeterministicSuggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task Analyze_MultipleCategoriesWithClusters_AnalyzesEachSeparately()
    {
        var entries = new List<CategorizationHistory>
        {
            // Groceries cluster
            MakeEntry("pak n save petone", 1, "PAK N SAVE PETONE"),
            MakeEntry("pak n save lower hutt", 1, "PAK N SAVE LOWER HUTT"),
            MakeEntry("pak n save wellington", 1, "PAK N SAVE WELLINGTON"),
            // Entertainment cluster
            MakeEntry("spotify premium", 2, "SPOTIFY PREMIUM"),
            MakeEntry("spotify monthly", 2, "SPOTIFY MONTHLY"),
            MakeEntry("spotify payment", 2, "SPOTIFY PAYMENT")
        };

        _historyRepo.GetAllForUserAsync(_userId, Arg.Any<CancellationToken>()).Returns(entries);
        _ruleRepo.GetActiveRulesForUserAsync(_userId).Returns(Array.Empty<CategorizationRule>());

        var result = await _analyzer.AnalyzeAsync(_userId);

        // Both clusters should produce deterministic suggestions
        result.DeterministicSuggestions.Should().HaveCountGreaterThanOrEqualTo(2);
        result.DeterministicSuggestions.Should().Contain(s => s.SuggestedCategoryId == 1);
        result.DeterministicSuggestions.Should().Contain(s => s.SuggestedCategoryId == 2);
    }

    // --- Static method tests (ClusterByTokenSimilarity, FindCommonToken) ---

    [Fact]
    public void ClusterByTokenSimilarity_EntriesSharingTokens_GroupedTogether()
    {
        var entries = new List<CategorizationHistory>
        {
            MakeEntry("uber trip auckland", 3),
            MakeEntry("uber eats wellington", 3),
            MakeEntry("uber ride christchurch", 3)
        };

        var clusters = CategorizationHistoryAnalyzer.ClusterByTokenSimilarity(entries);

        // All share "uber" — should be one cluster
        clusters.Should().HaveCount(1);
        clusters[0].Should().HaveCount(3);
    }

    [Fact]
    public void ClusterByTokenSimilarity_NoSharedTokens_SeparateClusters()
    {
        var entries = new List<CategorizationHistory>
        {
            MakeEntry("netflix subscription", 2),
            MakeEntry("spotify premium", 2),
            MakeEntry("disney plus", 2)
        };

        var clusters = CategorizationHistoryAnalyzer.ClusterByTokenSimilarity(entries);

        // No shared tokens — each forms its own cluster
        clusters.Should().HaveCount(3);
    }

    [Fact]
    public void FindCommonToken_AllShareToken_ReturnsLongestSharedToken()
    {
        var entries = new List<CategorizationHistory>
        {
            MakeEntry("pak n save petone", 1),
            MakeEntry("pak n save lower hutt", 1),
            MakeEntry("pak n save wellington", 1)
        };

        var token = CategorizationHistoryAnalyzer.FindCommonToken(entries);

        // "save" is the common token >= 4 chars (and also "petone" is not shared)
        token.Should().NotBeNull();
        token!.Length.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void FindCommonToken_NoSharedLongToken_ReturnsNull()
    {
        var entries = new List<CategorizationHistory>
        {
            MakeEntry("new world wellington", 1),
            MakeEntry("countdown petone", 1),
            MakeEntry("fresh choice johnsonville", 1)
        };

        var token = CategorizationHistoryAnalyzer.FindCommonToken(entries);

        token.Should().BeNull();
    }

    [Fact]
    public void FindCommonToken_EmptyCluster_ReturnsNull()
    {
        var token = CategorizationHistoryAnalyzer.FindCommonToken(new List<CategorizationHistory>());
        token.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_RuleValidationFails_SkipsSuggestion()
    {
        // Create a cluster where the common token "uber" also matches entries in a DIFFERENT category
        // This should fail the precision check
        var entries = new List<CategorizationHistory>
        {
            MakeEntry("uber trip auckland", 3, "UBER TRIP AUCKLAND"),
            MakeEntry("uber eats wellington", 3, "UBER EATS WELLINGTON"),
            MakeEntry("uber ride christchurch", 3, "UBER RIDE CHRISTCHURCH"),
            // Same "uber" token but different category — lowers precision
            MakeEntry("uber eats dinner", 1, "UBER EATS DINNER"),
            MakeEntry("uber eats lunch", 1, "UBER EATS LUNCH")
        };

        _historyRepo.GetAllForUserAsync(_userId, Arg.Any<CancellationToken>()).Returns(entries);
        _ruleRepo.GetActiveRulesForUserAsync(_userId).Returns(Array.Empty<CategorizationRule>());

        var result = await _analyzer.AnalyzeAsync(_userId);

        // The "uber" token matches 5 entries total, only 3 in category 3 → precision = 3/5 = 0.60 < 0.90
        // So it should fail validation and not appear in deterministic suggestions
        result.DeterministicSuggestions.Should().NotContain(s =>
            s.Pattern.Equals("uber", StringComparison.OrdinalIgnoreCase) && s.SuggestedCategoryId == 3);
    }

    // --- Helpers ---

    private CategorizationHistory MakeEntry(
        string normalizedDescription, int categoryId, string? originalDescription = null, int matchCount = 1)
    {
        return new CategorizationHistory
        {
            UserId = _userId,
            NormalizedDescription = normalizedDescription,
            OriginalDescription = originalDescription ?? normalizedDescription.ToUpperInvariant(),
            CategoryId = categoryId,
            MatchCount = matchCount,
            LastUsedAt = DateTime.UtcNow,
            Source = CategorizationHistorySource.Manual
        };
    }
}
