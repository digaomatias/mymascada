namespace MyMascada.Application.BackgroundJobs;

/// <summary>
/// Daily background job that checks every user's active budgets and sends
/// threshold/exceeded notifications for categories that have crossed their
/// alert limit. Idempotent per category per period.
/// </summary>
public interface IBudgetAlertJobService
{
    /// <summary>
    /// Processes all users that have at least one active budget.
    /// Scheduled to run daily.
    /// </summary>
    Task ProcessAllUsersAsync(CancellationToken ct = default);
}
