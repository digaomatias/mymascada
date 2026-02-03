using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.RuleSuggestions.Services;

/// <summary>
/// Core interface for rule suggestion analysis strategies
/// </summary>
public interface IRuleSuggestionAnalyzer
{
    /// <summary>
    /// Analyzes transactions to generate rule suggestions
    /// </summary>
    Task<List<PatternSuggestion>> AnalyzePatternsAsync(RuleAnalysisInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the analysis method name for tracking and display
    /// </summary>
    string AnalysisMethod { get; }

    /// <summary>
    /// Indicates if this analyzer requires AI services
    /// </summary>
    bool RequiresAI { get; }
}

/// <summary>
/// Input data for rule suggestion analysis
/// </summary>
public class RuleAnalysisInput
{
    public Guid UserId { get; set; }
    public List<Transaction> Transactions { get; set; } = new();
    public List<Category> AvailableCategories { get; set; } = new();
    public List<CategorizationRule> ExistingRules { get; set; } = new();
    public int MaxSuggestions { get; set; } = 10;
    public double MinConfidenceThreshold { get; set; } = 0.7;
}

/// <summary>
/// Configuration for rule suggestion analysis
/// </summary>
public class RuleAnalysisConfiguration
{
    /// <summary>
    /// Whether AI-powered analysis is enabled globally
    /// </summary>
    public bool IsAIAnalysisEnabled { get; set; } = true;

    /// <summary>
    /// Whether to use AI for this specific user (for pro/free tier differentiation)
    /// </summary>
    public bool UseAIForUser { get; set; } = true;

    /// <summary>
    /// Fallback to basic analysis if AI fails
    /// </summary>
    public bool FallbackToBasicOnAIFailure { get; set; } = true;

    /// <summary>
    /// Maximum AI API calls per user per day (cost control)
    /// </summary>
    public int MaxAICallsPerUserPerDay { get; set; } = 10;

    /// <summary>
    /// Minimum transactions required before using AI analysis
    /// </summary>
    public int MinTransactionsForAI { get; set; } = 50;
}

/// <summary>
/// Factory interface for creating appropriate analyzers based on configuration
/// </summary>
public interface IRuleSuggestionAnalyzerFactory
{
    /// <summary>
    /// Creates the appropriate analyzer based on configuration and user context
    /// </summary>
    Task<IRuleSuggestionAnalyzer> CreateAnalyzerAsync(Guid userId, RuleAnalysisConfiguration config);
}

/// <summary>
/// Service interface for tracking AI usage (cost control)
/// </summary>
public interface IAIUsageTracker
{
    /// <summary>
    /// Checks if user has remaining AI quota for today
    /// </summary>
    Task<bool> CanUseAIAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records AI usage for cost tracking
    /// </summary>
    Task RecordAIUsageAsync(Guid userId, string operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets remaining AI quota for user
    /// </summary>
    Task<int> GetRemainingQuotaAsync(Guid userId, CancellationToken cancellationToken = default);
}