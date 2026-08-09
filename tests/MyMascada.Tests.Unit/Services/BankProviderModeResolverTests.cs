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
            AppIdToken = "test-fake-app-token",
            AppSecret = "test-fake-app-secret"
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
