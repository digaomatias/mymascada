using MyMascada.Application.Features.BankConnections.DTOs;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Factory for resolving bank providers by their provider ID.
/// Auto-discovers all registered IBankProvider implementations via DI.
/// </summary>
public interface IBankProviderFactory
{
    /// <summary>
    /// Gets a bank provider by its unique identifier.
    /// Throws KeyNotFoundException if the provider is not registered.
    /// </summary>
    /// <param name="providerId">The unique identifier of the provider (e.g., "akahu")</param>
    /// <returns>The bank provider implementation</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no provider with the given ID is registered</exception>
    IBankProvider GetProvider(string providerId);

    /// <summary>
    /// Gets a bank provider by its unique identifier, or null if not found.
    /// </summary>
    /// <param name="providerId">The unique identifier of the provider (e.g., "akahu")</param>
    /// <returns>The bank provider implementation, or null if not found</returns>
    IBankProvider? GetProviderOrDefault(string providerId);

    /// <summary>
    /// Gets information about all available bank providers.
    /// </summary>
    /// <returns>A read-only list of provider information</returns>
    IReadOnlyList<BankProviderInfo> GetAvailableProviders();

    /// <summary>
    /// Checks if a provider with the given ID is available.
    /// </summary>
    /// <param name="providerId">The unique identifier of the provider</param>
    /// <returns>True if the provider is registered and available</returns>
    bool IsProviderAvailable(string providerId);
}
