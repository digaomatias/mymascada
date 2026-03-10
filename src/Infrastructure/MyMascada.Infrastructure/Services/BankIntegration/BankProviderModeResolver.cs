using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.DTOs;
using MyMascada.Infrastructure.Services.BankIntegration.Providers;

namespace MyMascada.Infrastructure.Services.BankIntegration;

public class BankProviderModeResolver : IBankProviderModeResolver
{
    private const string AkahuProviderId = "akahu";
    private const string PersonalMode = "personal_tokens";
    private const string HostedOAuthMode = "hosted_oauth";

    private readonly AkahuOptions _akahuOptions;

    public BankProviderModeResolver(IOptions<AkahuOptions> akahuOptions)
    {
        _akahuOptions = akahuOptions.Value;
    }

    public BankProviderModeResolution Resolve(string providerId)
    {
        if (string.Equals(providerId, AkahuProviderId, StringComparison.OrdinalIgnoreCase))
        {
            var hasHostedOAuth = !string.IsNullOrWhiteSpace(_akahuOptions.AppIdToken)
                && !string.IsNullOrWhiteSpace(_akahuOptions.AppSecret);

            var modes = new List<BankProviderAuthModeInfo>
            {
                new()
                {
                    ModeId = PersonalMode,
                    DisplayName = "Personal tokens",
                    RequiresUserCredentials = true
                }
            };

            if (hasHostedOAuth)
            {
                modes.Add(new BankProviderAuthModeInfo
                {
                    ModeId = HostedOAuthMode,
                    DisplayName = "MyMascada OAuth",
                    RequiresUserCredentials = false
                });
            }

            return new BankProviderModeResolution(
                AkahuProviderId,
                hasHostedOAuth ? HostedOAuthMode : PersonalMode,
                modes);
        }

        return new BankProviderModeResolution(
            providerId,
            PersonalMode,
            new[]
            {
                new BankProviderAuthModeInfo
                {
                    ModeId = PersonalMode,
                    DisplayName = "Personal tokens",
                    RequiresUserCredentials = true
                }
            });
    }
}
