using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.Services;
using MyMascada.Infrastructure.Services.Registration;

namespace MyMascada.WebAPI.Extensions;

/// <summary>
/// Registers IFeatureFlags (singleton) and IRegistrationStrategy (scoped)
/// based on configuration evaluated once at startup.
/// </summary>
public static class FeatureFlagsExtensions
{
    public static IServiceCollection AddFeatureFlags(this IServiceCollection services, IConfiguration configuration)
    {
        var aiCategorization = IsNonPlaceholder(configuration["LLM:OpenAI:ApiKey"], "YOUR_OPENAI_API_KEY");
        var emailNotifications = IsEmailConfigured(configuration);
        var googleOAuth = IsNonPlaceholder(configuration["Authentication:Google:ClientId"], "YOUR_GOOGLE_CLIENT_ID");
        var bankSync = configuration.GetValue<bool>("Akahu:Enabled");

        // Singleton — values never change after startup
        services.AddSingleton<IFeatureFlags>(new StartupFeatureFlags(
            aiCategorization,
            emailNotifications,
            googleOAuth,
            bankSync));

        // Registration strategy depends on email availability
        if (emailNotifications)
        {
            services.AddScoped<IRegistrationStrategy, EmailVerifiedRegistrationStrategy>();
        }
        else
        {
            services.AddScoped<IRegistrationStrategy, DirectRegistrationStrategy>();
        }

        return services;
    }

    /// <summary>
    /// Evaluates whether email is properly configured. Runs once at startup.
    /// This replaces the deleted EmailConfigurationEvaluator static class.
    /// </summary>
    internal static bool IsEmailConfigured(IConfiguration configuration)
    {
        var emailSection = configuration.GetSection(EmailOptions.SectionName);
        var enabled = emailSection.GetValue<bool>("Enabled");
        if (!enabled) return false;

        var defaultFrom = emailSection["DefaultFromEmail"];
        if (string.IsNullOrWhiteSpace(defaultFrom)) return false;

        var provider = emailSection["Provider"]?.ToLowerInvariant();
        return provider switch
        {
            "smtp" => !string.IsNullOrWhiteSpace(emailSection["Smtp:Host"]),
            "postmark" => !string.IsNullOrWhiteSpace(emailSection["Postmark:ServerToken"]),
            _ => false
        };
    }

    private static bool IsNonPlaceholder(string? value, string placeholder)
    {
        return !string.IsNullOrEmpty(value)
            && !string.Equals(value, placeholder, StringComparison.Ordinal);
    }
}
