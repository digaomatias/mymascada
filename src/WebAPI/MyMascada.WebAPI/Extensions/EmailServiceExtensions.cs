using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.Services.Email;
using MyMascada.Infrastructure.Services.Email.Providers;
using MyMascada.Infrastructure.Services.Email.Templates;

namespace MyMascada.WebAPI.Extensions;

/// <summary>
/// Extension methods for registering email services in the DI container.
/// Conditionally registers real providers or NoOp implementations based on configuration.
/// </summary>
public static class EmailServiceExtensions
{
    public static IServiceCollection AddEmailServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Always bind configuration options (used by other services even when email is disabled)
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<PasswordResetOptions>(configuration.GetSection(PasswordResetOptions.SectionName));
        services.Configure<EmailVerificationOptions>(configuration.GetSection(EmailVerificationOptions.SectionName));

        // Template service (always registered — NoOp factory never calls it, but DI graph requires it)
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();

        if (FeatureFlagsExtensions.IsEmailConfigured(configuration))
        {
            // Real providers — factory auto-discovers via IEnumerable<IEmailService>
            services.AddScoped<IEmailService, SmtpEmailService>();
            services.AddScoped<IEmailService, PostmarkEmailService>();
            services.AddScoped<IEmailServiceFactory, EmailServiceFactory>();
        }
        else
        {
            // NoOp — all email send calls return Success = false gracefully
            services.AddSingleton<NoOpEmailService>();
            services.AddSingleton<IEmailService>(sp => sp.GetRequiredService<NoOpEmailService>());
            services.AddSingleton<IEmailServiceFactory, NoOpEmailServiceFactory>();
        }

        // Health check for monitoring
        services.AddHealthChecks()
            .AddCheck<EmailHealthCheck>("email", tags: new[] { "ready" });

        return services;
    }
}
