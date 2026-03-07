namespace MyMascada.Application.BackgroundJobs;

/// <summary>
/// Background job service for processing expired budget periods.
/// Creates new periods for recurring budgets and marks non-recurring ones as completed.
/// </summary>
public interface IExpiredBudgetJobService
{
    /// <summary>
    /// Processes all users' expired budgets.
    /// Scheduled to run daily at 1:00 AM.
    /// </summary>
    /// <param name="performContext">Hangfire context for progress tracking (optional)</param>
    Task ProcessAllUsersAsync(object? performContext = null);
}
