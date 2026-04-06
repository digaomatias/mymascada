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
        string source,
        CancellationToken ct = default);
}
