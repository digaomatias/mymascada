using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Categorization.Services;

/// <summary>
/// Service for managing categorization candidates and their application to transactions
/// </summary>
public interface ICategorizationCandidatesService
{
    /// <summary>
    /// Gets pending candidates for specific transactions
    /// Returns them grouped by transaction ID for easy UI consumption
    /// </summary>
    Task<Dictionary<int, List<CategorizationCandidate>>> GetPendingCandidatesForTransactionsAsync(
        IEnumerable<int> transactionIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and stores categorization candidates from pipeline results
    /// </summary>
    Task<IEnumerable<CategorizationCandidate>> CreateCandidatesAsync(
        IEnumerable<CategorizationCandidate> candidates, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a candidate to its transaction (sets category and marks as auto-categorized)
    /// </summary>
    Task<bool> ApplyCandidateAsync(
        int candidateId, string appliedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a candidate (marks as rejected, doesn't affect transaction)
    /// </summary>
    Task<bool> RejectCandidateAsync(
        int candidateId, string rejectedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies multiple candidates in a batch operation
    /// </summary>
    Task<BatchCandidateResult> ApplyCandidatesBatchAsync(
        IEnumerable<int> candidateIds, string appliedBy, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects multiple candidates in a batch operation
    /// </summary>
    Task<BatchCandidateResult> RejectCandidatesBatchAsync(
        IEnumerable<int> candidateIds, string rejectedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Auto-applies high-confidence candidates based on configured thresholds
    /// Used primarily by Rules Handler for trusted suggestions
    /// </summary>
    Task<AutoApplyResult> AutoApplyHighConfidenceCandidatesAsync(
        IEnumerable<CategorizationCandidate> candidates, 
        string appliedBy,
        decimal confidenceThreshold = 0.95m,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about candidates for a user
    /// </summary>
    Task<CategorizationCandidateStats> GetCandidateStatsAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts candidates to the format expected by the frontend CategoryPicker
    /// </summary>
    Task<IEnumerable<AiCategorySuggestion>> ConvertCandidatesToAiSuggestionsAsync(
        IEnumerable<CategorizationCandidate> candidates, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of batch candidate operations
/// </summary>
public class BatchCandidateResult
{
    public int SuccessfulCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool IsSuccess => FailedCount == 0;
}

/// <summary>
/// Result of auto-apply operations
/// </summary>
public class AutoApplyResult
{
    public int AppliedCount { get; set; }
    public int RemainingCandidatesCount { get; set; }
    public List<int> AppliedCandidateIds { get; set; } = new();
    public List<int> AppliedTransactionIds { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool HasErrors => Errors.Any();
}

/// <summary>
/// AI category suggestion format expected by frontend
/// </summary>
public class AiCategorySuggestion
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public int CandidateId { get; set; }
    public bool CanAutoApply { get; set; }
}