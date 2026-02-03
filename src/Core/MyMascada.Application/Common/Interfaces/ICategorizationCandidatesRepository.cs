using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Repository interface for managing categorization candidates
/// </summary>
public interface ICategorizationCandidatesRepository
{
    /// <summary>
    /// Gets all pending candidates for a specific transaction
    /// </summary>
    Task<IEnumerable<CategorizationCandidate>> GetPendingCandidatesForTransactionAsync(
        int transactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all pending candidates for multiple transactions
    /// </summary>
    Task<IEnumerable<CategorizationCandidate>> GetPendingCandidatesForTransactionsAsync(
        IEnumerable<int> transactionIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all candidates for multiple transactions filtered by categorization method
    /// </summary>
    Task<IEnumerable<CategorizationCandidate>> GetCandidatesForTransactionsByMethodAsync(
        IEnumerable<int> transactionIds, string categorizationMethod, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all pending candidates for a user's transactions
    /// </summary>
    Task<IEnumerable<CategorizationCandidate>> GetPendingCandidatesForUserAsync(
        Guid userId, int limit = 500, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple candidates in a batch operation
    /// </summary>
    Task<IEnumerable<CategorizationCandidate>> AddCandidatesBatchAsync(
        IEnumerable<CategorizationCandidate> candidates, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a candidate by ID
    /// </summary>
    Task<CategorizationCandidate?> GetByIdAsync(
        int candidateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a candidate's status
    /// </summary>
    Task UpdateCandidateAsync(
        CategorizationCandidate candidate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks candidates as applied in a batch operation
    /// </summary>
    Task MarkCandidatesAsAppliedBatchAsync(
        IEnumerable<int> candidateIds, string appliedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks candidates as applied using bulk update (ExecuteUpdate) for better performance
    /// </summary>
    Task BulkMarkCandidatesAsAppliedAsync(
        IEnumerable<int> candidateIds, string appliedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks candidates as rejected in a batch operation
    /// </summary>
    Task MarkCandidatesAsRejectedBatchAsync(
        IEnumerable<int> candidateIds, string rejectedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets candidates grouped by transaction for easy UI display
    /// </summary>
    Task<Dictionary<int, List<CategorizationCandidate>>> GetCandidatesGroupedByTransactionAsync(
        IEnumerable<int> transactionIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes old rejected/applied candidates for cleanup
    /// </summary>
    Task CleanupOldCandidatesAsync(
        DateTime olderThan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about candidates for a user
    /// </summary>
    Task<CategorizationCandidateStats> GetCandidateStatsAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets transaction IDs that have pending candidates
    /// Used to prevent creating duplicate candidates
    /// </summary>
    Task<HashSet<int>> GetTransactionIdsWithPendingCandidatesAsync(
        IEnumerable<int> transactionIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about categorization candidates
/// </summary>
public class CategorizationCandidateStats
{
    public int TotalPending { get; set; }
    public int TotalApplied { get; set; }
    public int TotalRejected { get; set; }
    public decimal AverageConfidence { get; set; }
    public Dictionary<string, int> ByMethod { get; set; } = new();
    public Dictionary<string, int> ByStatus { get; set; } = new();
}