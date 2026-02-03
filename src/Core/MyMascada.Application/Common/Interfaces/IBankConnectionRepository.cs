using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Repository interface for managing bank connections.
/// </summary>
public interface IBankConnectionRepository
{
    /// <summary>
    /// Gets a bank connection by its ID.
    /// </summary>
    /// <param name="id">The bank connection ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The bank connection, or null if not found</returns>
    Task<BankConnection?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Gets the bank connection for a specific account.
    /// </summary>
    /// <param name="accountId">The account ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The bank connection, or null if not found</returns>
    Task<BankConnection?> GetByAccountIdAsync(int accountId, CancellationToken ct = default);

    /// <summary>
    /// Gets all bank connections for a user.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Collection of bank connections</returns>
    Task<IEnumerable<BankConnection>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets all active bank connections for a user.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Collection of active bank connections</returns>
    Task<IEnumerable<BankConnection>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Adds a new bank connection.
    /// </summary>
    /// <param name="bankConnection">The bank connection to add</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The created bank connection with generated ID</returns>
    Task<BankConnection> AddAsync(BankConnection bankConnection, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing bank connection.
    /// </summary>
    /// <param name="bankConnection">The bank connection to update</param>
    /// <param name="ct">Cancellation token</param>
    Task UpdateAsync(BankConnection bankConnection, CancellationToken ct = default);

    /// <summary>
    /// Deletes a bank connection by its ID.
    /// </summary>
    /// <param name="id">The bank connection ID to delete</param>
    /// <param name="ct">Cancellation token</param>
    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Checks if a bank connection exists by external account ID and provider.
    /// </summary>
    /// <param name="externalAccountId">The external account ID from the provider</param>
    /// <param name="providerId">The provider identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if a connection exists</returns>
    Task<bool> ExistsByExternalAccountIdAsync(string externalAccountId, string providerId, CancellationToken ct = default);

    /// <summary>
    /// Gets a bank connection by external account ID and provider.
    /// </summary>
    /// <param name="externalAccountId">The external account ID from the provider</param>
    /// <param name="providerId">The provider identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The bank connection, or null if not found</returns>
    Task<BankConnection?> GetByExternalAccountIdAsync(string externalAccountId, string providerId, CancellationToken ct = default);
}
