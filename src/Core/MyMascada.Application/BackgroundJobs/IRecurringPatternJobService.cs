namespace MyMascada.Application.BackgroundJobs;

/// <summary>
/// Service for managing recurring pattern background jobs.
/// Runs daily to detect patterns and process missed payments.
/// </summary>
public interface IRecurringPatternJobService
{
    /// <summary>
    /// Processes all users' recurring patterns - detects new patterns and handles missed payments.
    /// This is the main entry point called by Hangfire daily at 2:00 AM.
    /// </summary>
    /// <param name="performContext">Hangfire context for progress tracking (optional)</param>
    Task ProcessAllUsersAsync(object? performContext = null);

    /// <summary>
    /// Processes recurring patterns for a specific user.
    /// Useful for manual triggering or testing.
    /// </summary>
    /// <param name="userId">User ID to process</param>
    /// <param name="performContext">Hangfire context for progress tracking (optional)</param>
    Task ProcessUserAsync(Guid userId, object? performContext = null);
}
