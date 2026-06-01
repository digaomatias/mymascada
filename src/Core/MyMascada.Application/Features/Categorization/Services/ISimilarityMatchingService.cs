namespace MyMascada.Application.Features.Categorization.Services;

/// <summary>
/// Result of a similarity match against the user's categorization history.
/// </summary>
public record SimilarityMatch(
    int CategoryId,
    string CategoryName,
    decimal Confidence,
    string MatchType,         // "Exact" or "Fuzzy"
    string MatchedDescription // The normalized description from history that matched
);

public interface ISimilarityMatchingService
{
    /// <summary>
    /// Finds the best category match for a transaction description using the user's categorization history.
    /// Returns null if no match meets the minimum confidence threshold (0.60).
    ///
    /// Tier 1: Exact normalized match (fast DB lookup, high confidence).
    /// Tier 2: Token-overlap fuzzy match (in-memory, lower confidence).
    /// </summary>
    Task<SimilarityMatch?> FindBestMatchAsync(
        Guid userId, string normalizedDescription, CancellationToken ct = default);
}
