namespace MyMascada.Application.BackgroundJobs;

/// <summary>
/// Service for managing transaction categorization background jobs
/// </summary>
public interface ITransactionCategorizationJobService
{
    /// <summary>
    /// Enqueues transaction categorization for the specified transactions
    /// </summary>
    /// <param name="transactionIds">List of transaction IDs to categorize</param>
    /// <param name="userId">User ID for logging and scoping</param>
    /// <returns>Job ID for tracking</returns>
    string EnqueueCategorization(List<int> transactionIds, string userId);

    /// <summary>
    /// Enqueues transaction categorization as a continuation of another job.
    /// The categorization will only run after the parent job completes successfully.
    /// </summary>
    /// <param name="parentJobId">The job ID that must complete before categorization runs</param>
    /// <param name="transactionIds">List of transaction IDs to categorize</param>
    /// <param name="userId">User ID for logging and scoping</param>
    /// <returns>Job ID for tracking</returns>
    string EnqueueCategorizationAfter(string parentJobId, List<int> transactionIds, string userId);

    /// <summary>
    /// Processes transaction categorization (executed by Hangfire)
    /// </summary>
    /// <param name="transactionIds">List of transaction IDs to categorize</param>
    /// <param name="userId">User ID for logging and scoping</param>
    /// <param name="performContext">Hangfire context for progress tracking (optional)</param>
    Task ProcessCategorizationAsync(List<int> transactionIds, string userId, object? performContext = null);
}