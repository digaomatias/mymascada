using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Categorization.Services;

public class SimilarityMatchingService : ISimilarityMatchingService
{
    private readonly ICategorizationHistoryRepository _historyRepository;
    private readonly ILogger<SimilarityMatchingService> _logger;

    private const decimal MinimumCandidateConfidence = 0.60m;

    // Cache pre-tokenized history per user to avoid redundant DB queries and tokenization
    private Guid? _lastUserId;
    private IReadOnlyList<(CategorizationHistory Entry, HashSet<string> TokenSet)>? _cachedHistory;

    public SimilarityMatchingService(
        ICategorizationHistoryRepository historyRepository,
        ILogger<SimilarityMatchingService> logger)
    {
        _historyRepository = historyRepository;
        _logger = logger;
    }

    public async Task<SimilarityMatch?> FindBestMatchAsync(
        Guid userId, string normalizedDescription, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(normalizedDescription))
            return null;

        // Tier 1: Exact normalized match (single indexed DB lookup)
        var exactMatch = await _historyRepository.FindByNormalizedDescriptionAsync(userId, normalizedDescription, ct);
        if (exactMatch != null)
        {
            // Skip if the category was soft-deleted (navigation will be null due to query filter)
            if (exactMatch.Category == null)
            {
                _logger.LogDebug(
                    "Exact match found for category {CategoryId} but category is missing (soft-deleted?), skipping",
                    exactMatch.CategoryId);
            }
            else
            {
                var confidence = CalculateConfidence(exactMatch.MatchCount);

                _logger.LogDebug(
                    "Exact match found for category {CategoryId} ({CategoryName}), " +
                    "matchCount={MatchCount}, confidence={Confidence:F2}",
                    exactMatch.CategoryId, exactMatch.Category.Name,
                    exactMatch.MatchCount, confidence);

                return new SimilarityMatch(
                    exactMatch.CategoryId,
                    exactMatch.Category.Name,
                    confidence,
                    "Exact",
                    exactMatch.NormalizedDescription);
            }
        }

        // Tier 2: Token-overlap fuzzy matching
        var inputTokens = DescriptionNormalizer.ExtractTokens(normalizedDescription);
        if (inputTokens.Count == 0)
            return null;

        // Fetch and cache pre-tokenized history for this user (exclude soft-deleted categories)
        if (_lastUserId != userId || _cachedHistory == null)
        {
            var history = await _historyRepository.GetAllForUserAsync(userId, ct);
            _cachedHistory = history
                .Where(h => h.Category != null)
                .Select(h => (
                    Entry: h,
                    TokenSet: DescriptionNormalizer.ExtractTokens(h.NormalizedDescription)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase)))
                .Where(x => x.TokenSet.Count > 0)
                .ToList();
            _lastUserId = userId;
        }

        if (_cachedHistory.Count == 0)
            return null;

        SimilarityMatch? bestMatch = null;
        var inputTokenSet = inputTokens.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (entry, entryTokenSet) in _cachedHistory)
        {
            var sharedTokens = inputTokenSet.Count(t => entryTokenSet.Contains(t));
            if (sharedTokens < 2)
                continue;

            var maxTokens = Math.Max(inputTokenSet.Count, entryTokenSet.Count);
            var tokenOverlap = (decimal)sharedTokens / maxTokens;
            var baseConfidence = CalculateConfidence(entry.MatchCount);
            var finalConfidence = tokenOverlap * baseConfidence;

            if (finalConfidence < MinimumCandidateConfidence)
                continue;

            if (bestMatch == null || finalConfidence > bestMatch.Confidence)
            {
                bestMatch = new SimilarityMatch(
                    entry.CategoryId,
                    entry.Category!.Name,
                    finalConfidence,
                    "Fuzzy",
                    entry.NormalizedDescription);
            }
        }

        if (bestMatch != null)
        {
            _logger.LogDebug(
                "Fuzzy match found: category {CategoryId} ({CategoryName}), confidence={Confidence:F2}",
                bestMatch.CategoryId, bestMatch.CategoryName, bestMatch.Confidence);
        }

        return bestMatch;
    }

    /// <summary>
    /// Confidence scaling based on how many times the mapping has been confirmed:
    ///   1st = 0.70, 2nd = 0.80, 3rd = 0.85, 5+ = 0.90, 10+ = 0.95
    /// </summary>
    public void InvalidateCache()
    {
        _lastUserId = null;
        _cachedHistory = null;
    }

    internal static decimal CalculateConfidence(int matchCount) => matchCount switch
    {
        <= 0 => 0.70m,
        1 => 0.70m,
        2 => 0.80m,
        3 => 0.85m,
        >= 10 => 0.95m,
        >= 5 => 0.90m,
        _ => 0.85m // 4
    };
}
