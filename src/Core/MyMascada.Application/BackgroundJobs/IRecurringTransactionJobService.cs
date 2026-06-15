namespace MyMascada.Application.BackgroundJobs;

/// <summary>
/// Service for the daily user-created recurring transaction job.
/// Auto-creates due transactions or sends reminder notifications,
/// then advances each schedule's next due date.
/// </summary>
public interface IRecurringTransactionJobService
{
    /// <summary>
    /// Processes all due recurring transactions across all users.
    /// This is the main entry point called by Hangfire daily.
    /// Safe to run multiple times per day (idempotent per scheduled date).
    /// </summary>
    /// <param name="performContext">Hangfire context for progress tracking (optional)</param>
    Task ProcessAllDueAsync(object? performContext = null);
}
