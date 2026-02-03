using MyMascada.Infrastructure.Services.Health;

namespace MyMascada.WebAPI.Extensions;

/// <summary>
/// Extension methods for registering health check services in the DI container.
/// </summary>
public static class HealthCheckServiceExtensions
{
    /// <summary>
    /// Adds database, LLM service, and Akahu API health checks to the health check system.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddHealthCheckServices(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" })
            .AddCheck<LlmServiceHealthCheck>("llm-service", tags: new[] { "ready" })
            .AddCheck<AkahuHealthCheck>("akahu", tags: new[] { "ready" });

        return services;
    }
}
