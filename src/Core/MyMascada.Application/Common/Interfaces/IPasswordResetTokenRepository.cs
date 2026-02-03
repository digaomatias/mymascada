using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Repository interface for password reset token operations
/// </summary>
public interface IPasswordResetTokenRepository
{
    /// <summary>
    /// Gets a password reset token by its hash
    /// </summary>
    /// <param name="tokenHash">SHA-256 hash of the token</param>
    /// <returns>The token if found, null otherwise</returns>
    Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash);

    /// <summary>
    /// Gets the count of password reset requests for a user within a time window.
    /// Used for rate limiting.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="window">The time window to check</param>
    /// <returns>Number of requests within the window</returns>
    Task<int> GetRecentRequestCountAsync(Guid userId, TimeSpan window);

    /// <summary>
    /// Adds a new password reset token
    /// </summary>
    /// <param name="token">The token to add</param>
    Task AddAsync(PasswordResetToken token);

    /// <summary>
    /// Updates an existing password reset token
    /// </summary>
    /// <param name="token">The token to update</param>
    Task UpdateAsync(PasswordResetToken token);

    /// <summary>
    /// Invalidates all unused tokens for a user.
    /// Called when a new password reset is requested or password is changed.
    /// </summary>
    /// <param name="userId">The user ID</param>
    Task InvalidateAllForUserAsync(Guid userId);

    /// <summary>
    /// Deletes expired and used tokens older than the specified date.
    /// Used for periodic cleanup to prevent database bloat.
    /// </summary>
    Task<int> DeleteExpiredAndUsedTokensAsync(DateTime olderThan);
}
