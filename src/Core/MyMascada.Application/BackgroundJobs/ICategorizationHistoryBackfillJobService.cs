namespace MyMascada.Application.BackgroundJobs;

/// <summary>
/// One-time background job that populates CategorizationHistory from existing
/// categorized transactions. Safe to re-run (idempotent upserts).
/// </summary>
public interface ICategorizationHistoryBackfillJobService
{
    Task BackfillAllUsersAsync(CancellationToken ct = default);
}
