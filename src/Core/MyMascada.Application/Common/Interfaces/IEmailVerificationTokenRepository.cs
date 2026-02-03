using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Repository interface for email verification token operations
/// </summary>
public interface IEmailVerificationTokenRepository
{
    /// <summary>
    /// Gets an email verification token by its hash
    /// </summary>
    /// <param name="tokenHash">SHA-256 hash of the token</param>
    /// <returns>The token if found, null otherwise</returns>
    Task<EmailVerificationToken?> GetByTokenHashAsync(string tokenHash);

    /// <summary>
    /// Gets the count of verification email requests for a user within a time window.
    /// Used for rate limiting.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="window">The time window to check</param>
    /// <returns>Number of requests within the window</returns>
    Task<int> GetRecentRequestCountAsync(Guid userId, TimeSpan window);

    /// <summary>
    /// Adds a new email verification token
    /// </summary>
    /// <param name="token">The token to add</param>
    Task AddAsync(EmailVerificationToken token);

    /// <summary>
    /// Updates an existing email verification token
    /// </summary>
    /// <param name="token">The token to update</param>
    Task UpdateAsync(EmailVerificationToken token);

    /// <summary>
    /// Invalidates all unused tokens for a user.
    /// Called when a new verification email is requested or email is verified.
    /// </summary>
    /// <param name="userId">The user ID</param>
    Task InvalidateAllForUserAsync(Guid userId);
}
