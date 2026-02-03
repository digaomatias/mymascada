using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Repository interface for managing bank sync logs.
/// </summary>
public interface IBankSyncLogRepository
{
    /// <summary>
    /// Gets a sync log by its ID.
    /// </summary>
    /// <param name="id">The sync log ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The sync log, or null if not found</returns>
    Task<BankSyncLog?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Gets sync logs for a bank connection, ordered by most recent first.
    /// </summary>
    /// <param name="bankConnectionId">The bank connection ID</param>
    /// <param name="limit">Maximum number of logs to return (default 20)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Collection of sync logs</returns>
    Task<IEnumerable<BankSyncLog>> GetByBankConnectionIdAsync(int bankConnectionId, int limit = 20, CancellationToken ct = default);

    /// <summary>
    /// Gets the most recent sync log for a bank connection.
    /// </summary>
    /// <param name="bankConnectionId">The bank connection ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The most recent sync log, or null if none exist</returns>
    Task<BankSyncLog?> GetLatestByBankConnectionIdAsync(int bankConnectionId, CancellationToken ct = default);

    /// <summary>
    /// Adds a new sync log entry.
    /// </summary>
    /// <param name="syncLog">The sync log to add</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The created sync log with generated ID</returns>
    Task<BankSyncLog> AddAsync(BankSyncLog syncLog, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing sync log entry.
    /// </summary>
    /// <param name="syncLog">The sync log to update</param>
    /// <param name="ct">Cancellation token</param>
    Task UpdateAsync(BankSyncLog syncLog, CancellationToken ct = default);

    /// <summary>
    /// Gets sync statistics for a bank connection.
    /// </summary>
    /// <param name="bankConnectionId">The bank connection ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tuple containing total syncs, successful syncs, and total transactions imported</returns>
    Task<(int TotalSyncs, int SuccessfulSyncs, int TotalTransactionsImported)> GetSyncStatisticsAsync(int bankConnectionId, CancellationToken ct = default);
}
