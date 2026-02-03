using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Email.DTOs;

namespace MyMascada.Infrastructure.Services.Email;

/// <summary>
/// Factory for resolving email service providers.
/// Auto-discovers all registered IEmailService implementations via DI.
/// </summary>
public class EmailServiceFactory : IEmailServiceFactory
{
    private readonly Dictionary<string, IEmailService> _providers;
    private readonly EmailOptions _options;
    private readonly IApplicationLogger<EmailServiceFactory> _logger;

    public EmailServiceFactory(
        IEnumerable<IEmailService> providers,
        IOptions<EmailOptions> options,
        IApplicationLogger<EmailServiceFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
        _providers = providers.ToDictionary(
            p => p.ProviderId,
            p => p,
            StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation("EmailServiceFactory initialized with {Count} providers: {Providers}",
            _providers.Count, string.Join(", ", _providers.Keys));
    }

    /// <inheritdoc />
    public IEmailService GetDefaultProvider()
    {
        if (!_providers.TryGetValue(_options.Provider, out var provider))
        {
            _logger.LogError(null, "Configured email provider '{Provider}' not found. Available: {Available}",
                new { Provider = _options.Provider, Available = string.Join(", ", _providers.Keys) });
            throw new InvalidOperationException(
                $"Email provider '{_options.Provider}' is not registered. Available providers: {string.Join(", ", _providers.Keys)}");
        }
        return provider;
    }

    /// <inheritdoc />
    public IEmailService GetProvider(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            throw new ArgumentException("Provider ID cannot be null or empty", nameof(providerId));

        if (!_providers.TryGetValue(providerId, out var provider))
        {
            _logger.LogWarning("Email provider '{ProviderId}' not found. Available providers: {Available}",
                providerId, string.Join(", ", _providers.Keys));
            throw new KeyNotFoundException($"Email provider '{providerId}' is not registered. Available providers: {string.Join(", ", _providers.Keys)}");
        }

        return provider;
    }

    /// <inheritdoc />
    public IEmailService? GetProviderOrDefault(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            return null;

        _providers.TryGetValue(providerId, out var provider);
        return provider;
    }

    /// <inheritdoc />
    public IReadOnlyList<EmailProviderInfo> GetAvailableProviders()
    {
        return _providers.Values
            .Select(p => new EmailProviderInfo
            {
                ProviderId = p.ProviderId,
                DisplayName = p.DisplayName,
                IsDefault = p.ProviderId.Equals(_options.Provider, StringComparison.OrdinalIgnoreCase),
                SupportsAttachments = p.SupportsAttachments
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
