namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Service for seeding default categories for users.
/// Supports multiple locales for category names and descriptions.
/// </summary>
public interface ICategorySeedingService
{
    /// <summary>
    /// Creates default categories for a user in the specified locale.
    /// Will not create categories if the user already has them (unless force is true).
    /// </summary>
    /// <param name="userId">The user ID to create categories for</param>
    /// <param name="locale">Locale for category names (e.g., "en", "pt-BR"). Defaults to "en".</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of categories created</returns>
    Task<int> CreateDefaultCategoriesAsync(Guid userId, string locale = "en", CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user already has categories
    /// </summary>
    /// <param name="userId">The user ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user has categories, false otherwise</returns>
    Task<bool> UserHasCategoriesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the list of supported locales for category seeding.
    /// </summary>
    IReadOnlyList<string> GetAvailableLocales();
}
