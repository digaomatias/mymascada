using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.Repositories;
using MyMascada.Infrastructure.Services;
using MyMascada.Infrastructure.Services.BankIntegration;
using MyMascada.Infrastructure.Services.BankIntegration.Providers;
using MyMascada.Infrastructure.Services.Security;

namespace MyMascada.WebAPI.Extensions;

/// <summary>
/// Extension methods for registering bank provider services in the DI container.
/// </summary>
public static class BankProviderServiceExtensions
{
    /// <summary>
    /// Adds all bank provider related services to the service collection.
    /// This includes the provider factory, Akahu provider, sync service, and related repositories.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The application configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddBankProviderServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration - bind Akahu options from appsettings
        services.Configure<AkahuOptions>(configuration.GetSection(AkahuOptions.SectionName));

        // Security services
        services.AddScoped<ISettingsEncryptionService, SettingsEncryptionService>();

        // Core bank integration services
        services.AddScoped<IBankProviderFactory, BankProviderFactory>();
        services.AddScoped<IBankSyncService, BankSyncService>();

        // Repositories for bank integration
        services.AddScoped<IBankConnectionRepository, BankConnectionRepository>();
        services.AddScoped<IBankSyncLogRepository, BankSyncLogRepository>();
        services.AddScoped<IAkahuUserCredentialRepository, AkahuUserCredentialRepository>();
        services.AddScoped<IBankCategoryMappingRepository, BankCategoryMappingRepository>();

        // Bank category mapping service
        services.AddScoped<IBankCategoryMappingService, BankCategoryMappingService>();

        // Akahu provider - register HttpClient with named client pattern
        services.AddHttpClient<AkahuApiClient>();

        // Register the Akahu API client interface
        services.AddScoped<IAkahuApiClient>(sp => sp.GetRequiredService<AkahuApiClient>());

        // Register Akahu as a bank provider (factory will auto-discover via DI)
        services.AddScoped<IBankProvider, AkahuBankProvider>();

        return services;
    }
}
