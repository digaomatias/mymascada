using Microsoft.Extensions.Configuration;
using MyMascada.WebAPI.Extensions;

namespace MyMascada.Tests.Unit.Extensions;

public class FeatureFlagsExtensionsTests
{
    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static Dictionary<string, string?> ValidBaseConfig(string provider)
    {
        return new Dictionary<string, string?>
        {
            ["Email:Enabled"] = "true",
            ["Email:Provider"] = provider,
            ["Email:DefaultFromEmail"] = "no-reply@mymascada.com"
        };
    }

    [Fact]
    public void IsEmailConfigured_WhenDisabled_ReturnsFalse()
    {
        var config = ValidBaseConfig("resend");
        config["Email:Enabled"] = "false";
        config["Email:Resend:ApiKey"] = "re_test_key";

        var result = FeatureFlagsExtensions.IsEmailConfigured(BuildConfiguration(config));

        result.Should().BeFalse();
    }

    [Fact]
    public void IsEmailConfigured_WhenDefaultFromEmailMissing_ReturnsFalse()
    {
        var config = ValidBaseConfig("resend");
        config["Email:DefaultFromEmail"] = "";
        config["Email:Resend:ApiKey"] = "re_test_key";

        var result = FeatureFlagsExtensions.IsEmailConfigured(BuildConfiguration(config));

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("resend")]
    [InlineData("Resend")]
    [InlineData("RESEND")]
    public void IsEmailConfigured_ResendWithApiKey_ReturnsTrue(string provider)
    {
        var config = ValidBaseConfig(provider);
        config["Email:Resend:ApiKey"] = "re_test_key";

        var result = FeatureFlagsExtensions.IsEmailConfigured(BuildConfiguration(config));

        result.Should().BeTrue();
    }

    [Fact]
    public void IsEmailConfigured_ResendWithoutApiKey_ReturnsFalse()
    {
        var config = ValidBaseConfig("resend");

        var result = FeatureFlagsExtensions.IsEmailConfigured(BuildConfiguration(config));

        result.Should().BeFalse();
    }

    [Fact]
    public void IsEmailConfigured_SmtpWithHost_ReturnsTrue()
    {
        var config = ValidBaseConfig("smtp");
        config["Email:Smtp:Host"] = "mail.example.com";

        var result = FeatureFlagsExtensions.IsEmailConfigured(BuildConfiguration(config));

        result.Should().BeTrue();
    }

    [Fact]
    public void IsEmailConfigured_SmtpWithoutHost_ReturnsFalse()
    {
        var config = ValidBaseConfig("smtp");

        var result = FeatureFlagsExtensions.IsEmailConfigured(BuildConfiguration(config));

        result.Should().BeFalse();
    }

    [Fact]
    public void IsEmailConfigured_PostmarkWithServerToken_ReturnsTrue()
    {
        var config = ValidBaseConfig("postmark");
        config["Email:Postmark:ServerToken"] = "pm-test-token";

        var result = FeatureFlagsExtensions.IsEmailConfigured(BuildConfiguration(config));

        result.Should().BeTrue();
    }

    [Fact]
    public void IsEmailConfigured_PostmarkWithoutServerToken_ReturnsFalse()
    {
        var config = ValidBaseConfig("postmark");

        var result = FeatureFlagsExtensions.IsEmailConfigured(BuildConfiguration(config));

        result.Should().BeFalse();
    }

    [Fact]
    public void IsEmailConfigured_UnknownProvider_ReturnsFalse()
    {
        var config = ValidBaseConfig("sendgrid");
        config["Email:Resend:ApiKey"] = "re_test_key";

        var result = FeatureFlagsExtensions.IsEmailConfigured(BuildConfiguration(config));

        result.Should().BeFalse();
    }

    [Fact]
    public void IsEmailConfigured_MissingProviderWithSmtpHost_FallsBackToSmtp_ReturnsTrue()
    {
        // Provider omitted — EmailOptions defaults to "smtp" at runtime,
        // so the flag check must mirror that fallback.
        var config = new Dictionary<string, string?>
        {
            ["Email:Enabled"] = "true",
            ["Email:DefaultFromEmail"] = "no-reply@mymascada.com",
            ["Email:Smtp:Host"] = "mail.example.com"
        };

        var result = FeatureFlagsExtensions.IsEmailConfigured(BuildConfiguration(config));

        result.Should().BeTrue();
    }

    [Fact]
    public void IsEmailConfigured_MissingProviderWithoutSmtpHost_ReturnsFalse()
    {
        var config = new Dictionary<string, string?>
        {
            ["Email:Enabled"] = "true",
            ["Email:DefaultFromEmail"] = "no-reply@mymascada.com"
        };

        var result = FeatureFlagsExtensions.IsEmailConfigured(BuildConfiguration(config));

        result.Should().BeFalse();
    }

    [Fact]
    public void IsEmailConfigured_EmptyProvider_ReturnsFalse()
    {
        // An explicitly empty Provider binds to "" (overriding the options
        // default) and would fail the provider factory lookup at runtime.
        var config = ValidBaseConfig("");
        config["Email:Smtp:Host"] = "mail.example.com";

        var result = FeatureFlagsExtensions.IsEmailConfigured(BuildConfiguration(config));

        result.Should().BeFalse();
    }
}
