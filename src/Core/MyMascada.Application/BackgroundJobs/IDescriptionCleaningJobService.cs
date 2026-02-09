namespace MyMascada.Application.BackgroundJobs;

/// <summary>
/// Service for managing description cleaning background jobs
/// </summary>
public interface IDescriptionCleaningJobService
{
    /// <summary>
    /// Enqueues description cleaning for the specified transactions
    /// </summary>
    /// <param name="transactionIds">List of transaction IDs to clean descriptions for</param>
    /// <param name="userId">User ID for logging and scoping</param>
    /// <returns>Job ID for tracking</returns>
    string EnqueueDescriptionCleaning(List<int> transactionIds, string userId);

    /// <summary>
    /// Processes description cleaning (executed by Hangfire)
    /// </summary>
    /// <param name="transactionIds">List of transaction IDs to clean descriptions for</param>
    /// <param name="userId">User ID for logging and scoping</param>
    /// <param name="performContext">Hangfire context for progress tracking (optional)</param>
    Task ProcessDescriptionCleaningAsync(List<int> transactionIds, string userId, object? performContext = null);
}
