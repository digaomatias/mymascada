using MyMascada.Application.Features.Email.DTOs;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Factory for resolving email service providers.
/// Follows the same pattern as IBankProviderFactory.
/// </summary>
public interface IEmailServiceFactory
{
    /// <summary>
    /// Gets the configured default email provider based on EmailOptions.Provider setting.
    /// </summary>
    /// <returns>The default email service implementation</returns>
    /// <exception cref="InvalidOperationException">Thrown when the configured provider is not registered</exception>
    IEmailService GetDefaultProvider();

    /// <summary>
    /// Gets a specific provider by ID (for testing/debugging or explicit selection).
    /// </summary>
    /// <param name="providerId">The unique identifier of the provider (e.g., "smtp", "postmark")</param>
    /// <returns>The email service implementation</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no provider with the given ID is registered</exception>
    IEmailService GetProvider(string providerId);

    /// <summary>
    /// Gets a provider by ID or null if not found.
    /// </summary>
    /// <param name="providerId">The unique identifier of the provider</param>
    /// <returns>The email service implementation, or null if not found</returns>
    IEmailService? GetProviderOrDefault(string providerId);

    /// <summary>
    /// Gets provider info for all registered providers.
    /// </summary>
    /// <returns>A read-only list of provider information</returns>
    IReadOnlyList<EmailProviderInfo> GetAvailableProviders();

    /// <summary>
    /// Checks if a provider with the given ID is available.
    /// </summary>
    /// <param name="providerId">The unique identifier of the provider</param>
    /// <returns>True if the provider is registered and available</returns>
    bool IsProviderAvailable(string providerId);
}
