using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

public interface ICategorizationHistoryRepository
{
    /// <summary>
    /// Finds the history entry for a given user and normalized description (exact match).
    /// </summary>
    Task<CategorizationHistory?> FindByNormalizedDescriptionAsync(
        Guid userId, string normalizedDescription, CancellationToken ct = default);

    /// <summary>
    /// Returns all history entries for a user. Used for token-overlap fuzzy matching.
    /// Typically &lt; 500 entries per user.
    /// </summary>
    Task<IReadOnlyList<CategorizationHistory>> GetAllForUserAsync(
        Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Inserts or updates a history entry.
    /// If an entry with the same (UserId, NormalizedDescription) already exists:
    ///   - If it maps to the same category: increment MatchCount and update LastUsedAt
    ///   - If it maps to a different category: update to the new category, reset MatchCount to 1
    /// </summary>
    Task<CategorizationHistory> UpsertAsync(
        Guid userId,
        string normalizedDescription,
        string originalDescription,
        int categoryId,
        CategorizationHistorySource source,
        CancellationToken ct = default);

    /// <summary>
    /// Inserts or updates a history entry with an absolute match count (for backfill).
    /// Sets MatchCount = max(existing, count) to ensure idempotent reruns.
    /// </summary>
    Task<CategorizationHistory> UpsertWithAbsoluteCountAsync(
        Guid userId,
        string normalizedDescription,
        string originalDescription,
        int categoryId,
        int count,
        CategorizationHistorySource source,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all distinct user IDs that have categorized transactions (for backfill job).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetDistinctUserIdsWithCategorizedTransactionsAsync(CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
