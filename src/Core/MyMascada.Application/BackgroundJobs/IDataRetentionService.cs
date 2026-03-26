namespace MyMascada.Application.BackgroundJobs;

/// <summary>
/// Background job service for enforcing data retention policies.
/// Cleans up expired data according to configured retention periods.
/// </summary>
public interface IDataRetentionService
{
    /// <summary>
    /// Cleans up AI chat messages older than the configured retention period.
    /// Scheduled to run daily at 3:30 AM.
    /// </summary>
    Task CleanupExpiredChatMessagesAsync();
}
