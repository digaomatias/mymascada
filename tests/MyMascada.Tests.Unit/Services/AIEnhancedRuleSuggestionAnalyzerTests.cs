using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RuleSuggestions.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Tests.Unit.Services;

public class AIEnhancedRuleSuggestionAnalyzerTests
{
    private readonly BasicRuleSuggestionAnalyzer _basicAnalyzer;
    private readonly ICategorizationHistoryAnalyzer _historyAnalyzer;
    private readonly ILlmCategorizationService _llmService;
    private readonly IAIUsageTracker _usageTracker;
    private readonly AIEnhancedRuleSuggestionAnalyzer _analyzer;
    private readonly Guid _userId = Guid.NewGuid();

    public AIEnhancedRuleSuggestionAnalyzerTests()
    {
        var categoryRepo = Substitute.For<ICategoryRepository>();
        categoryRepo.GetByUserIdAsync(Arg.Any<Guid>()).Returns(new List<Category>());
        categoryRepo.GetSystemCategoriesAsync().Returns(new List<Category>
        {
            new() { Id = 1, Name = "Groceries" },
            new() { Id = 2, Name = "Entertainment" }
        });

        _basicAnalyzer = new BasicRuleSuggestionAnalyzer(categoryRepo);
        _historyAnalyzer = Substitute.For<ICategorizationHistoryAnalyzer>();
        _llmService = Substitute.For<ILlmCategorizationService>();
        _usageTracker = Substitute.For<IAIUsageTracker>();

        _analyzer = new AIEnhancedRuleSuggestionAnalyzer(
            _basicAnalyzer, _historyAnalyzer, _llmService, _usageTracker);
    }

    [Fact]
    public void AnalysisMethod_ReturnsHistoryBasedName()
    {
        _analyzer.AnalysisMethod.Should().Be("AI-Enhanced History Analysis");
    }

    [Fact]
    public void RequiresAI_ReturnsTrue()
    {
        _analyzer.RequiresAI.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzePatterns_IncludesDeterministicSuggestionsFromHistory()
    {
        var deterministicSuggestion = new PatternSuggestion
        {
            Pattern = "pak n save",
            SuggestedCategoryId = 1,
            SuggestedCategoryName = "Groceries",
            ConfidenceScore = 0.85,
            DetectionMethod = "History Pattern Analysis"
        };

        _historyAnalyzer.AnalyzeAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new HistoryAnalysisResult
            {
                DeterministicSuggestions = new List<PatternSuggestion> { deterministicSuggestion },
                AmbiguousClusters = new List<AmbiguousCluster>()
            });

        var input = CreateInput();
        var result = await _analyzer.AnalyzePatternsAsync(input);

        result.Should().Contain(s => s.Pattern == "pak n save" && s.DetectionMethod == "History Pattern Analysis");
    }

    [Fact]
    public async Task AnalyzePatterns_AmbiguousClusters_CallsAIWhenQuotaAvailable()
    {
        var cluster = new AmbiguousCluster
        {
            CategoryId = 1,
            CategoryName = "Groceries",
            Descriptions = new List<string> { "NEW WORLD WELLINGTON", "COUNTDOWN PETONE" },
            NormalizedDescriptions = new List<string> { "new world wellington", "countdown petone" }
        };

        _historyAnalyzer.AnalyzeAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new HistoryAnalysisResult
            {
                DeterministicSuggestions = new List<PatternSuggestion>(),
                AmbiguousClusters = new List<AmbiguousCluster> { cluster }
            });

        _usageTracker.CanUseAIAsync(_userId, Arg.Any<CancellationToken>()).Returns(true);
        _llmService.SendPromptAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("{\"pattern\": \"WORLD\", \"ruleType\": \"Contains\", \"confidence\": 0.85, \"reasoning\": \"test\"}");

        var input = CreateInput();
        var result = await _analyzer.AnalyzePatternsAsync(input);

        await _llmService.Received(1).SendPromptAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _usageTracker.Received(1).RecordAIUsageAsync(_userId, "rule_suggestions_history", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnalyzePatterns_AmbiguousClusters_SkipsAIWhenQuotaExceeded()
    {
        var cluster = new AmbiguousCluster
        {
            CategoryId = 1,
            CategoryName = "Groceries",
            Descriptions = new List<string> { "NEW WORLD", "COUNTDOWN" }
        };

        _historyAnalyzer.AnalyzeAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new HistoryAnalysisResult
            {
                DeterministicSuggestions = new List<PatternSuggestion>(),
                AmbiguousClusters = new List<AmbiguousCluster> { cluster }
            });

        _usageTracker.CanUseAIAsync(_userId, Arg.Any<CancellationToken>()).Returns(false);

        var input = CreateInput();
        await _analyzer.AnalyzePatternsAsync(input);

        await _llmService.DidNotReceive().SendPromptAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnalyzePatterns_AIReturnsValidJSON_CreatesPatternSuggestion()
    {
        var cluster = new AmbiguousCluster
        {
            CategoryId = 2,
            CategoryName = "Entertainment",
            Descriptions = new List<string> { "DISNEY PLUS", "DISNEY+" }
        };

        _historyAnalyzer.AnalyzeAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new HistoryAnalysisResult
            {
                DeterministicSuggestions = new List<PatternSuggestion>(),
                AmbiguousClusters = new List<AmbiguousCluster> { cluster }
            });

        _usageTracker.CanUseAIAsync(_userId, Arg.Any<CancellationToken>()).Returns(true);
        _llmService.SendPromptAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("{\"pattern\": \"DISNEY\", \"ruleType\": \"Contains\", \"confidence\": 0.90, \"reasoning\": \"All descriptions contain Disney\"}");

        var input = CreateInput();
        var result = await _analyzer.AnalyzePatternsAsync(input);

        result.Should().Contain(s =>
            s.Pattern == "DISNEY" &&
            s.SuggestedCategoryId == 2 &&
            s.DetectionMethod == "AI Cluster Analysis");
    }

    [Fact]
    public async Task AnalyzePatterns_AIReturnsInvalidJSON_DoesNotThrow()
    {
        var cluster = new AmbiguousCluster
        {
            CategoryId = 1,
            CategoryName = "Groceries",
            Descriptions = new List<string> { "NEW WORLD", "COUNTDOWN" }
        };

        _historyAnalyzer.AnalyzeAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new HistoryAnalysisResult
            {
                DeterministicSuggestions = new List<PatternSuggestion>(),
                AmbiguousClusters = new List<AmbiguousCluster> { cluster }
            });

        _usageTracker.CanUseAIAsync(_userId, Arg.Any<CancellationToken>()).Returns(true);
        _llmService.SendPromptAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("This is not JSON at all");

        var input = CreateInput();
        var act = () => _analyzer.AnalyzePatternsAsync(input);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AnalyzePatterns_AIPatternConflictsWithExistingRule_Filtered()
    {
        var cluster = new AmbiguousCluster
        {
            CategoryId = 2,
            CategoryName = "Entertainment",
            Descriptions = new List<string> { "NETFLIX" }
        };

        _historyAnalyzer.AnalyzeAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new HistoryAnalysisResult
            {
                DeterministicSuggestions = new List<PatternSuggestion>(),
                AmbiguousClusters = new List<AmbiguousCluster> { cluster }
            });

        _usageTracker.CanUseAIAsync(_userId, Arg.Any<CancellationToken>()).Returns(true);
        _llmService.SendPromptAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("{\"pattern\": \"NETFLIX\", \"ruleType\": \"Contains\", \"confidence\": 0.95, \"reasoning\": \"test\"}");

        var input = CreateInput();
        // Add an existing rule that matches the AI suggestion
        input.ExistingRules.Add(new CategorizationRule
        {
            Pattern = "NETFLIX", Type = RuleType.Contains, CategoryId = 2
        });

        var result = await _analyzer.AnalyzePatternsAsync(input);

        // AI suggestion should be filtered out since an identical rule already exists
        result.Should().NotContain(s => s.Pattern == "NETFLIX" && s.DetectionMethod == "AI Cluster Analysis");
    }

    [Fact]
    public async Task AnalyzePatterns_LimitsAIClustersTo5()
    {
        var clusters = Enumerable.Range(1, 10).Select(i => new AmbiguousCluster
        {
            CategoryId = 1,
            CategoryName = "Groceries",
            Descriptions = new List<string> { $"STORE {i}" }
        }).ToList();

        _historyAnalyzer.AnalyzeAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new HistoryAnalysisResult
            {
                DeterministicSuggestions = new List<PatternSuggestion>(),
                AmbiguousClusters = clusters
            });

        _usageTracker.CanUseAIAsync(_userId, Arg.Any<CancellationToken>()).Returns(true);
        _llmService.SendPromptAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("{\"pattern\": \"STORE\", \"ruleType\": \"Contains\", \"confidence\": 0.85, \"reasoning\": \"test\"}");

        var input = CreateInput();
        await _analyzer.AnalyzePatternsAsync(input);

        // Should only call AI for max 5 clusters
        await _llmService.Received(5).SendPromptAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private RuleAnalysisInput CreateInput()
    {
        return new RuleAnalysisInput
        {
            UserId = _userId,
            Transactions = new List<Transaction>(),
            AvailableCategories = new List<Category>
            {
                new() { Id = 1, Name = "Groceries" },
                new() { Id = 2, Name = "Entertainment" }
            },
            ExistingRules = new List<CategorizationRule>(),
            MaxSuggestions = 10,
            MinConfidenceThreshold = 0.7
        };
    }
}
