using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;

namespace MyMascada.Infrastructure.Services.BankIntegration;

/// <summary>
/// Factory for resolving bank providers by ID.
/// Auto-discovers all registered IBankProvider implementations via DI.
/// </summary>
public class BankProviderFactory : IBankProviderFactory
{
    private readonly Dictionary<string, IBankProvider> _providers;
    private readonly IApplicationLogger<BankProviderFactory> _logger;

    public BankProviderFactory(
        IEnumerable<IBankProvider> providers,
        IApplicationLogger<BankProviderFactory> logger)
    {
        _logger = logger;
        _providers = providers.ToDictionary(
            p => p.ProviderId,
            p => p,
            StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation("BankProviderFactory initialized with {Count} providers: {Providers}",
            _providers.Count, string.Join(", ", _providers.Keys));
    }

    /// <inheritdoc />
    public IBankProvider GetProvider(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            throw new ArgumentException("Provider ID cannot be null or empty", nameof(providerId));

        if (!_providers.TryGetValue(providerId, out var provider))
        {
            _logger.LogWarning("Bank provider '{ProviderId}' not found. Available providers: {Available}",
                providerId, string.Join(", ", _providers.Keys));
            throw new InvalidOperationException($"Bank provider '{providerId}' is not registered. Available providers: {string.Join(", ", _providers.Keys)}");
        }

        return provider;
    }

    /// <inheritdoc />
    public IBankProvider? GetProviderOrDefault(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            return null;

        _providers.TryGetValue(providerId, out var provider);
        return provider;
    }

    /// <inheritdoc />
    public IReadOnlyList<BankProviderInfo> GetAvailableProviders()
    {
        return _providers.Values
            .Select(p => new BankProviderInfo
            {
                ProviderId = p.ProviderId,
                DisplayName = p.DisplayName,
                SupportsWebhooks = p.SupportsWebhooks,
                SupportsBalanceFetch = p.SupportsBalanceFetch
            })
            .OrderBy(p => p.DisplayName)
            .ToList();
    }

    /// <inheritdoc />
    public bool IsProviderAvailable(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            return false;

        return _providers.ContainsKey(providerId);
    }
}
