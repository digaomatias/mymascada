namespace MyMascada.Application.BackgroundJobs;

/// <summary>
/// Service for retrying failed Akahu token revocations.
/// Runs periodically to pick up credentials flagged with pending revocations
/// and attempts to revoke the tokens again.
/// </summary>
public interface ITokenRevocationRetryJobService
{
    /// <summary>
    /// Retries all pending token revocations.
    /// Called by Hangfire on a recurring schedule.
    /// </summary>
    Task RetryPendingRevocationsAsync();
}
