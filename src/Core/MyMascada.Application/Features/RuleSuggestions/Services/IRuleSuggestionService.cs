using MyMascada.Application.Features.RuleSuggestions.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.RuleSuggestions.Services;

/// <summary>
/// Service interface for generating and managing rule suggestions
/// </summary>
public interface IRuleSuggestionService
{
    /// <summary>
    /// Generates rule suggestions for a user based on their transaction patterns
    /// </summary>
    Task<List<RuleSuggestion>> GenerateSuggestionsAsync(Guid userId, int maxSuggestions = 10, double minConfidence = 0.7, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets existing rule suggestions for a user
    /// </summary>
    Task<List<RuleSuggestion>> GetSuggestionsAsync(Guid userId, bool includeSamples = true);

    /// <summary>
    /// Accepts a rule suggestion and converts it to an actual categorization rule
    /// </summary>
    Task<int> AcceptSuggestionAsync(int suggestionId, Guid userId, string? customName = null, string? customDescription = null, int? priority = null);

    /// <summary>
    /// Rejects/dismisses a rule suggestion
    /// </summary>
    Task RejectSuggestionAsync(int suggestionId, Guid userId);

    /// <summary>
    /// Gets summary statistics for rule suggestions
    /// </summary>
    Task<RuleSuggestionsSummaryDto> GetSummaryAsync(Guid userId);

    /// <summary>
    /// Checks whether rule suggestion generation should be triggered for a user.
    /// Returns true if the threshold is met: >= 10 manually categorized AND >= 3 uncovered patterns.
    /// </summary>
    Task<bool> ShouldGenerateRuleSuggestionsAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Service interface for pattern detection algorithms
/// </summary>
public interface IPatternDetectionService
{
    /// <summary>
    /// Analyzes transactions to find keyword-based patterns
    /// </summary>
    Task<List<PatternSuggestion>> DetectKeywordPatternsAsync(List<Transaction> transactions, int minOccurrences = 3);

    /// <summary>
    /// Uses AI to detect complex patterns in transaction descriptions
    /// </summary>
    Task<List<PatternSuggestion>> DetectAiPatternsAsync(List<Transaction> transactions);
}

/// <summary>
/// Represents a detected pattern that could become a rule
/// </summary>
public class PatternSuggestion
{
    public string Pattern { get; set; } = string.Empty;
    public int SuggestedCategoryId { get; set; }
    public string SuggestedCategoryName { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public RuleType SuggestedRuleType { get; set; } = RuleType.Contains;
    public List<Transaction> MatchingTransactions { get; set; } = new();
    public string DetectionMethod { get; set; } = string.Empty;
    public string? Reasoning { get; set; }
}