using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;

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
        CategorizationHistorySource source,
        CancellationToken ct = default)
    {
        var normalized = DescriptionNormalizer.Normalize(description);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            _logger.LogDebug(
                "Skipping history recording — normalized description is empty for '{DescriptionPreview}'",
                Sanitize(description));
            return;
        }

        try
        {
            var entry = await _historyRepository.UpsertAsync(
                userId, normalized, description, categoryId, source, ct);
            await _historyRepository.SaveChangesAsync(ct);

            _logger.LogDebug(
                "Recorded categorization history: category {CategoryId}, " +
                "matchCount={MatchCount}, source={Source}",
                categoryId, entry.MatchCount, source);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to record categorization history for '{DescriptionPreview}' → category {CategoryId}",
                Sanitize(description), categoryId);
        }
    }

    public async Task RecordCategorizationBatchAsync(
        IEnumerable<CategorizationHistoryEvent> events,
        CancellationToken ct = default)
    {
        var eventList = events.ToList();
        if (eventList.Count == 0) return;

        try
        {
            var processedCount = 0;
            foreach (var evt in eventList)
            {
                var normalized = DescriptionNormalizer.Normalize(evt.Description);
                if (string.IsNullOrWhiteSpace(normalized)) continue;

                await _historyRepository.UpsertAsync(
                    evt.UserId, normalized, evt.Description, evt.CategoryId, evt.Source, ct);
                processedCount++;
            }

            await _historyRepository.SaveChangesAsync(ct);
            _logger.LogDebug("Batch recorded {ProcessedCount}/{TotalCount} categorization history entries",
                processedCount, eventList.Count);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to batch record {Count} categorization history entries",
                eventList.Count);
        }
    }

    private static string Sanitize(string description)
    {
        if (string.IsNullOrEmpty(description)) return "[empty]";
        const int previewLength = 8;
        return description.Length <= previewLength
            ? "[redacted]"
            : description[..previewLength] + "...";
    }
}
