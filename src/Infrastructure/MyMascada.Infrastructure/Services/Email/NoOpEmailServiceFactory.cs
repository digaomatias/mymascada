using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Email.DTOs;

namespace MyMascada.Infrastructure.Services.Email;

/// <summary>
/// Null Object implementation of IEmailServiceFactory used when email is not configured.
/// Always returns the NoOpEmailService so consumers don't need conditional logic.
/// </summary>
public class NoOpEmailServiceFactory : IEmailServiceFactory
{
    private readonly NoOpEmailService _noOpService;

    public NoOpEmailServiceFactory(NoOpEmailService noOpService)
    {
        _noOpService = noOpService;
    }

    public IEmailService GetDefaultProvider() => _noOpService;

    public IEmailService GetProvider(string providerId)
        => throw new KeyNotFoundException(
            $"Email provider '{providerId}' is not available. Email is not configured for this instance.");

    public IEmailService? GetProviderOrDefault(string providerId) => null;

    public IReadOnlyList<EmailProviderInfo> GetAvailableProviders() => Array.Empty<EmailProviderInfo>();

    public bool IsProviderAvailable(string providerId) => false;
}
