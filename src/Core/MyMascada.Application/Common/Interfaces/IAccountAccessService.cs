namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Central authorization service for account access. All repository queries
/// should use this service instead of filtering by userId directly.
/// Scoped lifetime with per-request caching to avoid repeated DB hits.
/// </summary>
public interface IAccountAccessService
{
    /// <summary>
    /// Returns the set of account IDs the user can access (owned + accepted shares).
    /// Cached per-request.
    /// </summary>
    Task<IReadOnlySet<int>> GetAccessibleAccountIdsAsync(Guid userId);

    /// <summary>
    /// Checks if the user has read access to the given account (ownership or accepted share).
    /// </summary>
    Task<bool> CanAccessAccountAsync(Guid userId, int accountId);

    /// <summary>
    /// Checks if the user can modify the account (owner or Manager role share).
    /// </summary>
    Task<bool> CanModifyAccountAsync(Guid userId, int accountId);

    /// <summary>
    /// Checks if the user is the account owner (Account.UserId == userId).
    /// Used for operations like account deletion, bank connection management, and sharing management.
    /// </summary>
    Task<bool> IsOwnerAsync(Guid userId, int accountId);

    /// <summary>
    /// Returns the owner's UserId for a given account.
    /// Used when a sharee needs to operate in the owner's context (e.g., categorization).
    /// </summary>
    Task<Guid?> GetAccountOwnerIdAsync(int accountId);
}
