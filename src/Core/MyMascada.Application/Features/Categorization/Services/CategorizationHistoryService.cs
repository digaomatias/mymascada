using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.Categorization.Services;

public class CategorizationHistoryService : ICategorizationHistoryService
{
    private readonly ICategorizationHistoryRepository _historyRepository;
    private readonly ILogger<CategorizationHistoryService> _logger;

    public CategorizationHistoryService(
        ICategorizationHistoryRepository historyRepository,
        ILogger<CategorizationHistoryService> logger)
    {
        _historyRepository = historyRepository;
        _logger = logger;
    }

    public async Task RecordCategorizationAsync(
        Guid userId,
        string description,
        int categoryId,
        string source,
        CancellationToken ct = default)
    {
        var normalized = DescriptionNormalizer.Normalize(description);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            _logger.LogDebug("Skipping history recording — normalized description is empty for '{Description}'", description);
            return;
        }

        try
        {
            var entry = await _historyRepository.UpsertAsync(
                userId, normalized, description, categoryId, source, ct);
            await _historyRepository.SaveChangesAsync(ct);

            _logger.LogDebug(
                "Recorded categorization history: '{Normalized}' → category {CategoryId}, " +
                "matchCount={MatchCount}, source={Source}",
                normalized, categoryId, entry.MatchCount, source);
        }
        catch (Exception ex)
        {
            // History recording is non-critical — log and continue
            _logger.LogWarning(ex,
                "Failed to record categorization history for '{Description}' → category {CategoryId}",
                description, categoryId);
        }
    }
}
