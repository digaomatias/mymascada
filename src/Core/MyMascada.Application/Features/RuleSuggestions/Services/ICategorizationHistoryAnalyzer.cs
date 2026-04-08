using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.RuleSuggestions.Services;

/// <summary>
/// Analyzes a user's categorization history to find clusters of similar descriptions
/// and generates rule suggestions — deterministic (common token) first, AI only for ambiguous clusters.
/// </summary>
public interface ICategorizationHistoryAnalyzer
{
    /// <summary>
    /// Analyzes history entries grouped by category, clusters by token similarity,
    /// checks for existing rule coverage, and returns suggestions for uncovered clusters.
    /// </summary>
    Task<HistoryAnalysisResult> AnalyzeAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Result of categorization history analysis.
/// </summary>
public class HistoryAnalysisResult
{
    /// <summary>
    /// Deterministic suggestions generated without AI (common token patterns).
    /// </summary>
    public List<PatternSuggestion> DeterministicSuggestions { get; set; } = new();

    /// <summary>
    /// Clusters that need AI analysis (no obvious common token).
    /// Each cluster contains the category info and the descriptions to analyze.
    /// </summary>
    public List<AmbiguousCluster> AmbiguousClusters { get; set; } = new();

    /// <summary>
    /// Total number of history entries analyzed.
    /// </summary>
    public int TotalEntriesAnalyzed { get; set; }

    /// <summary>
    /// Number of clusters already covered by existing rules (skipped).
    /// </summary>
    public int CoveredClusterCount { get; set; }
}

/// <summary>
/// A cluster of similar descriptions within the same category that has no obvious
/// common token pattern — requires AI analysis to generate a rule.
/// </summary>
public class AmbiguousCluster
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public List<string> Descriptions { get; set; } = new();
    public List<string> NormalizedDescriptions { get; set; } = new();
    public int TotalMatchCount { get; set; }
}
