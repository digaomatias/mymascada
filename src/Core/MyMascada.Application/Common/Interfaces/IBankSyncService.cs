using MyMascada.Application.Features.BankConnections.DTOs;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Service for orchestrating bank synchronization operations.
/// Coordinates between bank providers and the import analysis pipeline.
/// </summary>
public interface IBankSyncService
{
    /// <summary>
    /// Synchronizes a single bank connection, fetching and importing new transactions.
    /// </summary>
    /// <param name="bankConnectionId">The ID of the bank connection to sync</param>
    /// <param name="syncType">The type of sync being performed (manual, scheduled, webhook, initial)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result of the synchronization operation</returns>
    Task<BankSyncResult> SyncAccountAsync(int bankConnectionId, BankSyncType syncType, CancellationToken ct = default);

    /// <summary>
    /// Synchronizes all active bank connections for a user.
    /// </summary>
    /// <param name="userId">The user ID whose connections should be synced</param>
    /// <param name="syncType">The type of sync being performed</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Collection of sync results, one per connection</returns>
    Task<IEnumerable<BankSyncResult>> SyncAllConnectionsAsync(Guid userId, BankSyncType syncType, CancellationToken ct = default);
}
