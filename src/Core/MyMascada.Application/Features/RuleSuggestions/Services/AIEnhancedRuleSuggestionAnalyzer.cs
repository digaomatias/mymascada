using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using System.Text.Json;

namespace MyMascada.Application.Features.RuleSuggestions.Services;

/// <summary>
/// AI-enhanced rule suggestion analyzer that combines basic analysis with AI insights
/// </summary>
public class AIEnhancedRuleSuggestionAnalyzer : IRuleSuggestionAnalyzer
{
    private readonly BasicRuleSuggestionAnalyzer _basicAnalyzer;
    private readonly ILlmCategorizationService _llmService;
    private readonly IAIUsageTracker _usageTracker;

    public string AnalysisMethod => "AI-Enhanced Pattern Analysis";
    public bool RequiresAI => true;

    public AIEnhancedRuleSuggestionAnalyzer(
        BasicRuleSuggestionAnalyzer basicAnalyzer,
        ILlmCategorizationService llmService,
        IAIUsageTracker usageTracker)
    {
        _basicAnalyzer = basicAnalyzer;
        _llmService = llmService;
        _usageTracker = usageTracker;
    }

    public async Task<List<PatternSuggestion>> AnalyzePatternsAsync(RuleAnalysisInput input, CancellationToken cancellationToken = default)
    {
        var allSuggestions = new List<PatternSuggestion>();

        try
        {
            // 1. Always run basic analysis first (fast and reliable)
            var basicSuggestions = await _basicAnalyzer.AnalyzePatternsAsync(input, cancellationToken);
            allSuggestions.AddRange(basicSuggestions);

            // 2. Check if user can use AI
            var canUseAI = await _usageTracker.CanUseAIAsync(input.UserId, cancellationToken);
            if (!canUseAI)
            {
                return FilterAndCombineSuggestions(allSuggestions, input.MaxSuggestions, input.MinConfidenceThreshold);
            }

            // 3. Run AI analysis for uncategorized transactions
            var aiSuggestions = await RunAIAnalysis(input, cancellationToken);
            allSuggestions.AddRange(aiSuggestions);

            // 4. Record AI usage
            await _usageTracker.RecordAIUsageAsync(input.UserId, "rule_suggestions", cancellationToken);
        }
        catch (Exception ex)
        {
            // Log error but don't fail - return basic suggestions
            System.Diagnostics.Debug.WriteLine($"AI analysis failed: {ex.Message}");
            // In production, use proper logging
        }

        return FilterAndCombineSuggestions(allSuggestions, input.MaxSuggestions, input.MinConfidenceThreshold);
    }

    /// <summary>
    /// Runs AI analysis for complex pattern detection
    /// </summary>
    private async Task<List<PatternSuggestion>> RunAIAnalysis(RuleAnalysisInput input, CancellationToken cancellationToken)
    {
        var suggestions = new List<PatternSuggestion>();

        // Focus on uncategorized transactions for AI analysis
        var uncategorizedTransactions = input.Transactions
            .Where(t => !t.CategoryId.HasValue && !string.IsNullOrWhiteSpace(t.Description))
            .OrderByDescending(t => t.TransactionDate)
            .Take(30) // Limit for cost control
            .ToList();

        if (uncategorizedTransactions.Count < 3)
            return suggestions;

        try
        {
            // Get available categories for AI context
            var categoryContext = input.AvailableCategories
                .Select(c => $"{c.Id}:{c.Name}")
                .ToList();

            // Create AI prompt for rule suggestions
            var prompt = CreateRuleSuggestionPrompt(uncategorizedTransactions, categoryContext);
            
            var aiResponse = await _llmService.SendPromptAsync(prompt, cancellationToken);
            
            // Parse AI response and create suggestions
            var aiSuggestions = await ParseAIResponse(aiResponse, input);
            suggestions.AddRange(aiSuggestions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AI rule analysis failed: {ex.Message}");
            // Return empty list but don't fail the entire process
        }

        return suggestions;
    }

    /// <summary>
    /// Creates a focused prompt for AI rule suggestion analysis
    /// </summary>
    private string CreateRuleSuggestionPrompt(List<Transaction> transactions, List<string> categories)
    {
        var transactionSamples = transactions
            .Take(20)
            .Select(t => $"- {t.Description} (${Math.Abs(t.Amount):F2})")
            .ToList();

        return $@"Analyze these transaction descriptions to suggest automation rules for categorization:

TRANSACTIONS:
{string.Join("\n", transactionSamples)}

AVAILABLE CATEGORIES:
{string.Join(", ", categories)}

TASK:
Find patterns that could be automated into categorization rules. Look for:
1. Recurring merchant names (Netflix, Starbucks, etc.)
2. Transaction type patterns (ATM, transfers, subscriptions)
3. Description patterns that indicate specific categories

RESPONSE FORMAT (JSON):
{{
  ""suggestions"": [
    {{
      ""pattern"": ""keyword or phrase to match"",
      ""categoryId"": ""numeric category ID"",
      ""categoryName"": ""category name"",
      ""confidence"": 0.85,
      ""reasoning"": ""why this pattern makes sense"",
      ""matchingDescriptions"": [""example1"", ""example2""]
    }}
  ]
}}

RULES:
- Only suggest patterns with confidence > 0.8
- Maximum 5 suggestions
- Focus on clear, actionable patterns
- Ensure categoryId matches available categories";
    }

    /// <summary>
    /// Parses AI response and converts to PatternSuggestions
    /// </summary>
    private async Task<List<PatternSuggestion>> ParseAIResponse(string aiResponse, RuleAnalysisInput input)
    {
        var suggestions = new List<PatternSuggestion>();

        try
        {
            // Try to parse JSON response
            var jsonStart = aiResponse.IndexOf('{');
            var jsonEnd = aiResponse.LastIndexOf('}') + 1;
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = aiResponse.Substring(jsonStart, jsonEnd - jsonStart);
                var response = JsonSerializer.Deserialize<AIRuleSuggestionResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (response?.Suggestions != null)
                {
                    foreach (var aiSuggestion in response.Suggestions)
                    {
                        // Validate the suggestion
                        if (IsValidAISuggestion(aiSuggestion, input))
                        {
                            var matchingTransactions = FindMatchingTransactions(aiSuggestion.Pattern, input.Transactions);
                            
                            suggestions.Add(new PatternSuggestion
                            {
                                Pattern = aiSuggestion.Pattern,
                                SuggestedCategoryId = aiSuggestion.CategoryId,
                                SuggestedCategoryName = aiSuggestion.CategoryName,
                                ConfidenceScore = aiSuggestion.Confidence,
                                MatchingTransactions = matchingTransactions,
                                DetectionMethod = "AI Pattern Recognition",
                                Reasoning = aiSuggestion.Reasoning
                            });
                        }
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to parse AI response as JSON: {ex.Message}");
            // Try fallback parsing or use heuristics
            suggestions.AddRange(ParseAIResponseFallback(aiResponse, input));
        }

        return suggestions;
    }

    /// <summary>
    /// Fallback parsing when JSON parsing fails
    /// </summary>
    private List<PatternSuggestion> ParseAIResponseFallback(string aiResponse, RuleAnalysisInput input)
    {
        var suggestions = new List<PatternSuggestion>();

        // Simple pattern-based parsing as fallback
        // Look for common patterns in the AI response
        var lines = aiResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            // Look for patterns like "NETFLIX -> Entertainment (90% confidence)"
            if (line.Contains("->") && line.Contains("%"))
            {
                try
                {
                    var parts = line.Split(new[] { "->" }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var pattern = parts[0].Trim().Trim('"', '\'');
                        var categoryPart = parts[1].Trim();
                        
                        // Extract confidence if present
                        var confidenceMatch = System.Text.RegularExpressions.Regex.Match(categoryPart, @"(\d+)%");
                        var confidence = confidenceMatch.Success ? double.Parse(confidenceMatch.Groups[1].Value) / 100.0 : 0.8;
                        
                        if (confidence >= 0.8)
                        {
                            // Find matching category
                            var categoryName = categoryPart.Split('(')[0].Trim();
                            var category = input.AvailableCategories.FirstOrDefault(c => 
                                c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
                            
                            if (category != null)
                            {
                                var matchingTransactions = FindMatchingTransactions(pattern, input.Transactions);
                                
                                suggestions.Add(new PatternSuggestion
                                {
                                    Pattern = pattern,
                                    SuggestedCategoryId = category.Id,
                                    SuggestedCategoryName = category.Name,
                                    ConfidenceScore = confidence,
                                    MatchingTransactions = matchingTransactions,
                                    DetectionMethod = "AI Pattern Recognition (Fallback)",
                                    Reasoning = $"AI identified pattern '{pattern}' for {category.Name} category"
                                });
                            }
                        }
                    }
                }
                catch
                {
                    // Skip malformed lines
                    continue;
                }
            }
        }

        return suggestions;
    }

    /// <summary>
    /// Validates AI suggestion before adding to results
    /// </summary>
    private bool IsValidAISuggestion(AISuggestionItem suggestion, RuleAnalysisInput input)
    {
        // Check if pattern is meaningful
        if (string.IsNullOrWhiteSpace(suggestion.Pattern) || suggestion.Pattern.Length < 2)
            return false;

        // Check if confidence is reasonable
        if (suggestion.Confidence < 0.8 || suggestion.Confidence > 1.0)
            return false;

        // Check if category exists
        var categoryExists = input.AvailableCategories.Any(c => c.Id == suggestion.CategoryId);
        if (!categoryExists)
            return false;

        // Check if rule doesn't already exist
        var ruleExists = input.ExistingRules.Any(r => 
            r.Pattern.Equals(suggestion.Pattern, StringComparison.OrdinalIgnoreCase) &&
            r.CategoryId == suggestion.CategoryId);
        
        return !ruleExists;
    }

    /// <summary>
    /// Finds transactions that match a given pattern
    /// </summary>
    private List<Transaction> FindMatchingTransactions(string pattern, List<Transaction> transactions)
    {
        return transactions
            .Where(t => !string.IsNullOrWhiteSpace(t.Description) && 
                       t.Description.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            .Take(5) // Limit samples
            .ToList();
    }

    /// <summary>
    /// Filters and combines suggestions from multiple sources
    /// </summary>
    private List<PatternSuggestion> FilterAndCombineSuggestions(List<PatternSuggestion> suggestions, int maxSuggestions, double minConfidence)
    {
        // Remove duplicates based on pattern and category
        var uniqueSuggestions = suggestions
            .GroupBy(s => new { Pattern = s.Pattern.ToLowerInvariant(), CategoryId = s.SuggestedCategoryId })
            .Select(group => group.OrderByDescending(s => s.ConfidenceScore).First())
            .ToList();

        // Filter by confidence and rank
        return uniqueSuggestions
            .Where(s => s.ConfidenceScore >= minConfidence)
            .OrderByDescending(s => s.ConfidenceScore)
            .ThenByDescending(s => s.MatchingTransactions.Count)
            .ThenBy(s => s.DetectionMethod == "AI Pattern Recognition" ? 0 : 1) // Prefer AI suggestions when confidence is equal
            .Take(maxSuggestions)
            .ToList();
    }
}

/// <summary>
/// AI response structure for JSON parsing
/// </summary>
public class AIRuleSuggestionResponse
{
    public List<AISuggestionItem> Suggestions { get; set; } = new();
}

public class AISuggestionItem
{
    public string Pattern { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public List<string> MatchingDescriptions { get; set; } = new();
}
