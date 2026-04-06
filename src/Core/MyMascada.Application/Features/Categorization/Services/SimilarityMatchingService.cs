using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Categorization.Services;

public class SimilarityMatchingService : ISimilarityMatchingService
{
    private readonly ICategorizationHistoryRepository _historyRepository;
    private readonly ILogger<SimilarityMatchingService> _logger;

    private const decimal MinimumCandidateConfidence = 0.60m;

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
            var confidence = CalculateConfidence(exactMatch.MatchCount);

            _logger.LogDebug(
                "Exact match found for '{Description}': category {CategoryId} ({CategoryName}), " +
                "matchCount={MatchCount}, confidence={Confidence:F2}",
                normalizedDescription, exactMatch.CategoryId, exactMatch.Category?.Name,
                exactMatch.MatchCount, confidence);

            return new SimilarityMatch(
                exactMatch.CategoryId,
                exactMatch.Category?.Name ?? "Unknown",
                confidence,
                "Exact",
                exactMatch.NormalizedDescription);
        }

        // Tier 2: Token-overlap fuzzy matching
        var inputTokens = DescriptionNormalizer.ExtractTokens(normalizedDescription);
        if (inputTokens.Count == 0)
            return null;

        var allHistory = await _historyRepository.GetAllForUserAsync(userId, ct);
        if (allHistory.Count == 0)
            return null;

        SimilarityMatch? bestMatch = null;

        foreach (var entry in allHistory)
        {
            var entryTokens = DescriptionNormalizer.ExtractTokens(entry.NormalizedDescription);
            if (entryTokens.Count == 0)
                continue;

            var sharedTokens = inputTokens.Intersect(entryTokens, StringComparer.OrdinalIgnoreCase).Count();
            if (sharedTokens < 2)
                continue;

            var maxTokens = Math.Max(inputTokens.Count, entryTokens.Count);
            var tokenOverlap = (decimal)sharedTokens / maxTokens;
            var baseConfidence = CalculateConfidence(entry.MatchCount);
            var finalConfidence = tokenOverlap * baseConfidence;

            if (finalConfidence < MinimumCandidateConfidence)
                continue;

            if (bestMatch == null || finalConfidence > bestMatch.Confidence)
            {
                bestMatch = new SimilarityMatch(
                    entry.CategoryId,
                    entry.Category?.Name ?? "Unknown",
                    finalConfidence,
                    "Fuzzy",
                    entry.NormalizedDescription);
            }
        }

        if (bestMatch != null)
        {
            _logger.LogDebug(
                "Fuzzy match found for '{Description}': category {CategoryId} ({CategoryName}), " +
                "matched='{MatchedDesc}', confidence={Confidence:F2}",
                normalizedDescription, bestMatch.CategoryId, bestMatch.CategoryName,
                bestMatch.MatchedDescription, bestMatch.Confidence);
        }

        return bestMatch;
    }

    /// <summary>
    /// Confidence scaling based on how many times the mapping has been confirmed:
    ///   1st = 0.70, 2nd = 0.80, 3rd = 0.85, 5+ = 0.90, 10+ = 0.95
    /// </summary>
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
