using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Repository interface for managing Akahu user credentials.
/// Each user can have at most one set of Akahu credentials (one Personal App).
/// </summary>
public interface IAkahuUserCredentialRepository
{
    /// <summary>
    /// Gets the Akahu credentials for a user.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The user's Akahu credentials, or null if not set up</returns>
    Task<AkahuUserCredential?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets the Akahu credentials by ID.
    /// </summary>
    /// <param name="id">The credential ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The credentials, or null if not found</returns>
    Task<AkahuUserCredential?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Checks if a user has Akahu credentials configured.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if credentials exist for the user</returns>
    Task<bool> HasCredentialsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Adds new Akahu credentials for a user.
    /// Will fail if user already has credentials (use Update instead).
    /// </summary>
    /// <param name="credential">The credentials to add</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The created credentials with generated ID</returns>
    Task<AkahuUserCredential> AddAsync(AkahuUserCredential credential, CancellationToken ct = default);

    /// <summary>
    /// Updates existing Akahu credentials.
    /// </summary>
    /// <param name="credential">The credentials to update</param>
    /// <param name="ct">Cancellation token</param>
    Task UpdateAsync(AkahuUserCredential credential, CancellationToken ct = default);

    /// <summary>
    /// Deletes a user's Akahu credentials.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="ct">Cancellation token</param>
    Task DeleteByUserIdAsync(Guid userId, CancellationToken ct = default);
}
