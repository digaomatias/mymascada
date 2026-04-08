using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Categorization.Services;

/// <summary>
/// Records categorization events into the user's categorization history.
/// Called from manual categorization, candidate approval, and rule auto-apply paths.
/// </summary>
public interface ICategorizationHistoryService
{
    /// <summary>
    /// Records a categorization event, upserting the (userId, normalizedDescription) → categoryId mapping.
    /// </summary>
    Task RecordCategorizationAsync(
        Guid userId,
        string description,
        int categoryId,
        CategorizationHistorySource source,
        CancellationToken ct = default);

    /// <summary>
    /// Records multiple categorization events in a single batch (one SaveChanges call).
    /// </summary>
    Task RecordCategorizationBatchAsync(
        IEnumerable<CategorizationHistoryEvent> events,
        CancellationToken ct = default);
}

/// <summary>
/// DTO for batch categorization history recording.
/// </summary>
public record CategorizationHistoryEvent(
    Guid UserId,
    string Description,
    int CategoryId,
    CategorizationHistorySource Source);
