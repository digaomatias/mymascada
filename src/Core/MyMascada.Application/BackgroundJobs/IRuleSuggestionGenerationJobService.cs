namespace MyMascada.Application.BackgroundJobs;

/// <summary>
/// Weekly background job that checks trigger conditions per user and generates
/// rule suggestions when thresholds are met.
/// </summary>
public interface IRuleSuggestionGenerationJobService
{
    /// <summary>
    /// Processes all users — checks trigger conditions and generates suggestions.
    /// Called weekly by Hangfire.
    /// </summary>
    Task ProcessAllUsersAsync(CancellationToken ct = default);
}
