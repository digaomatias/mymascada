using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using System.Text.Json;

namespace MyMascada.Application.Features.RuleSuggestions.Services;

/// <summary>
/// AI-enhanced rule suggestion analyzer that combines history-based cluster analysis
/// with focused AI prompts for ambiguous clusters.
/// </summary>
public class AIEnhancedRuleSuggestionAnalyzer : IRuleSuggestionAnalyzer
{
    private readonly BasicRuleSuggestionAnalyzer _basicAnalyzer;
    private readonly ICategorizationHistoryAnalyzer _historyAnalyzer;
    private readonly ILlmCategorizationService _llmService;
    private readonly IAIUsageTracker _usageTracker;
    private readonly ILogger<AIEnhancedRuleSuggestionAnalyzer> _logger;

    private const int MaxAiClustersToProcess = 5;
    private const double MinAiSuggestionConfidence = 0.80;

    public string AnalysisMethod => "AI-Enhanced History Analysis";
    public bool RequiresAI => true;

    public AIEnhancedRuleSuggestionAnalyzer(
        BasicRuleSuggestionAnalyzer basicAnalyzer,
        ICategorizationHistoryAnalyzer historyAnalyzer,
        ILlmCategorizationService llmService,
        IAIUsageTracker usageTracker,
        ILogger<AIEnhancedRuleSuggestionAnalyzer> logger)
    {
        _basicAnalyzer = basicAnalyzer;
        _historyAnalyzer = historyAnalyzer;
        _llmService = llmService;
        _usageTracker = usageTracker;
        _logger = logger;
    }

    public async Task<List<PatternSuggestion>> AnalyzePatternsAsync(RuleAnalysisInput input, CancellationToken cancellationToken = default)
    {
        var allSuggestions = new List<PatternSuggestion>();

        // 1. Run history-based cluster analysis (deterministic suggestions + ambiguous clusters)
        var historyResult = await _historyAnalyzer.AnalyzeAsync(input.UserId, cancellationToken);
        allSuggestions.AddRange(historyResult.DeterministicSuggestions);

        // 2. Always include basic transaction-based analysis
        var basicSuggestions = await _basicAnalyzer.AnalyzePatternsAsync(input, cancellationToken);
        allSuggestions.AddRange(basicSuggestions);

        // 3. Use AI for ambiguous clusters (if quota allows)
        if (historyResult.AmbiguousClusters.Count > 0)
        {
            try
            {
                var canUseAI = await _usageTracker.CanUseAIAsync(input.UserId, cancellationToken);
                if (canUseAI)
                {
                    var aiSuggestions = await AnalyzeAmbiguousClustersAsync(
                        historyResult.AmbiguousClusters, input, cancellationToken);
                    allSuggestions.AddRange(aiSuggestions);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI cluster analysis failed, returning deterministic + basic suggestions only");
            }
        }

        return FilterAndCombineSuggestions(allSuggestions, input.MaxSuggestions, input.MinConfidenceThreshold);
    }

    /// <summary>
    /// Sends focused AI prompts for each ambiguous cluster (~$0.005/call).
    /// </summary>
    private async Task<List<PatternSuggestion>> AnalyzeAmbiguousClustersAsync(
        List<AmbiguousCluster> clusters,
        RuleAnalysisInput input,
        CancellationToken cancellationToken)
    {
        var suggestions = new List<PatternSuggestion>();

        foreach (var cluster in clusters.Take(MaxAiClustersToProcess))
        {
            try
            {
                var prompt = CreateClusterPrompt(cluster);
                var aiResponse = await _llmService.SendPromptAsync(prompt, cancellationToken);
                await _usageTracker.RecordAIUsageAsync(input.UserId, "rule_suggestions_history", cancellationToken);
                var clusterSuggestions = ParseClusterResponse(aiResponse, cluster, input);
                suggestions.AddRange(clusterSuggestions);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI cluster analysis failed for category '{CategoryName}'", cluster.CategoryName);
            }
        }

        return suggestions;
    }

    /// <summary>
    /// Creates a focused prompt for a single ambiguous cluster.
    /// Much smaller than per-transaction prompts — costs ~$0.005.
    /// </summary>
    private static string CreateClusterPrompt(AmbiguousCluster cluster)
    {
        var descriptions = cluster.Descriptions
            .Take(15)
            .Select(d => $"- {d}")
            .ToList();

        return $@"These transaction descriptions are all categorized as '{cluster.CategoryName}' by the user:

{string.Join("\n", descriptions)}

Suggest ONE categorization rule pattern that would match most of these descriptions.

RESPONSE FORMAT (JSON):
{{
  ""pattern"": ""keyword or phrase to match"",
  ""ruleType"": ""Contains"",
  ""confidence"": 0.85,
  ""reasoning"": ""why this pattern works""
}}

RULES:
- Pattern must be a single keyword or short phrase (not a regex)
- ruleType must be one of: Contains, StartsWith, EndsWith, Equals
- Only suggest if confidence > {MinAiSuggestionConfidence}
- Focus on the most distinctive shared element";
    }

    /// <summary>
    /// Parses AI response for a single cluster.
    /// </summary>
    private List<PatternSuggestion> ParseClusterResponse(
        string aiResponse, AmbiguousCluster cluster, RuleAnalysisInput input)
    {
        var suggestions = new List<PatternSuggestion>();

        try
        {
            var jsonStart = aiResponse.IndexOf('{');
            var jsonEnd = aiResponse.LastIndexOf('}') + 1;

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = aiResponse.Substring(jsonStart, jsonEnd - jsonStart);
                var response = JsonSerializer.Deserialize<AIClusterSuggestion>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (response != null && IsValidClusterSuggestion(response, cluster, input))
                {
                    suggestions.Add(new PatternSuggestion
                    {
                        Pattern = response.Pattern,
                        SuggestedCategoryId = cluster.CategoryId,
                        SuggestedCategoryName = cluster.CategoryName,
                        ConfidenceScore = response.Confidence,
                        SuggestedRuleType = ParseRuleType(response.RuleType),
                        MatchingTransactions = new List<Transaction>(),
                        DetectionMethod = "AI Cluster Analysis",
                        Reasoning = response.Reasoning
                    });
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to parse AI response for cluster '{CategoryName}'", cluster.CategoryName);
        }

        return suggestions;
    }

    private static bool IsValidClusterSuggestion(
        AIClusterSuggestion suggestion, AmbiguousCluster cluster, RuleAnalysisInput input)
    {
        if (string.IsNullOrWhiteSpace(suggestion.Pattern) || suggestion.Pattern.Length < 2)
            return false;

        if (suggestion.Confidence < MinAiSuggestionConfidence || suggestion.Confidence > 1.0)
            return false;

        // Check the pattern doesn't already exist as a rule with the same type
        var parsedType = ParseRuleType(suggestion.RuleType);
        return !input.ExistingRules.Any(r =>
            r.Pattern.Equals(suggestion.Pattern, StringComparison.OrdinalIgnoreCase) &&
            r.CategoryId == cluster.CategoryId &&
            r.Type == parsedType);
    }

    private static RuleType ParseRuleType(string ruleType)
    {
        return ruleType?.ToLowerInvariant() switch
        {
            "contains" => RuleType.Contains,
            "startswith" => RuleType.StartsWith,
            "endswith" => RuleType.EndsWith,
            "equals" => RuleType.Equals,
            _ => RuleType.Contains
        };
    }

    private static List<PatternSuggestion> FilterAndCombineSuggestions(
        List<PatternSuggestion> suggestions, int maxSuggestions, double minConfidence)
    {
        return suggestions
            .GroupBy(s => new { Pattern = s.Pattern.ToLowerInvariant(), CategoryId = s.SuggestedCategoryId, s.SuggestedRuleType })
            .Select(group => group.OrderByDescending(s => s.ConfidenceScore).First())
            .Where(s => s.ConfidenceScore >= minConfidence)
            .OrderByDescending(s => s.ConfidenceScore)
            .ThenByDescending(s => s.MatchingTransactions.Count)
            .Take(maxSuggestions)
            .ToList();
    }
}

/// <summary>
/// AI response structure for single-cluster analysis.
/// </summary>
public class AIClusterSuggestion
{
    public string Pattern { get; set; } = string.Empty;
    public string RuleType { get; set; } = "Contains";
    public double Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

