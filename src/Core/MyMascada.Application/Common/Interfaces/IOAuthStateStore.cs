namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Stores and validates OAuth state parameters server-side to prevent CSRF attacks.
/// State is bound to a specific user and is single-use with automatic expiry.
/// </summary>
public interface IOAuthStateStore
{
    /// <summary>
    /// Stores the OAuth state parameter for a user with automatic expiry.
    /// </summary>
    Task StoreAsync(Guid userId, string state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and consumes the OAuth state parameter. Returns true if valid.
    /// The state is removed after successful validation (single-use).
    /// </summary>
    Task<bool> ValidateAndConsumeAsync(Guid userId, string state, CancellationToken cancellationToken = default);
}
