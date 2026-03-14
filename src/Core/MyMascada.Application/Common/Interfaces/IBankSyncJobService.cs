using MyMascada.Application.Features.BankConnections.DTOs;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Enqueues and tracks bank synchronization background jobs.
/// </summary>
public interface IBankSyncJobService
{
    /// <summary>
    /// Enqueues a sync job for a single bank connection.
    /// </summary>
    BankSyncJobAcceptedDto EnqueueConnectionSync(Guid userId, int connectionId);

    /// <summary>
    /// Enqueues a sync job for multiple bank connections.
    /// </summary>
    BankSyncJobAcceptedDto EnqueueAllConnectionsSync(Guid userId, IReadOnlyCollection<int> connectionIds);

    /// <summary>
    /// Gets the current status of a background sync job for the given user.
    /// </summary>
    BankSyncJobStatusDto GetStatus(string jobId, Guid userId);
}
