using FluentAssertions;
using Microsoft.Extensions.Options;
using MyMascada.Infrastructure.Services.BankIntegration;
using MyMascada.Infrastructure.Services.BankIntegration.Providers;

namespace MyMascada.Tests.Unit.Services;

public class BankProviderModeResolverTests
{
    [Fact]
    public void Resolve_Akahu_WithHostedOAuthSecrets_ReturnsHostedOAuthDefault()
    {
        var options = Options.Create(new AkahuOptions
        {
            AppIdToken = "app_token_123",
            AppSecret = "secret_123"
        });

        var resolver = new BankProviderModeResolver(options);

        var result = resolver.Resolve("akahu");

        result.DefaultMode.Should().Be("hosted_oauth");
        result.SupportedModes.Select(m => m.ModeId).Should().Contain(new[] { "personal_tokens", "hosted_oauth" });
    }

    [Fact]
    public void Resolve_Akahu_WithoutHostedOAuthSecrets_ReturnsPersonalDefault()
    {
        var options = Options.Create(new AkahuOptions
        {
            AppIdToken = "",
            AppSecret = ""
        });

        var resolver = new BankProviderModeResolver(options);

        var result = resolver.Resolve("akahu");

        result.DefaultMode.Should().Be("personal_tokens");
        result.SupportedModes.Select(m => m.ModeId).Should().Equal("personal_tokens");
    }
}
