using HealthChecks.NpgSql;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MyMascada.Infrastructure.Data;
using MyMascada.Infrastructure.Services.Health;

namespace MyMascada.WebAPI.Extensions;

/// <summary>
/// Extension methods for registering health check services in the DI container.
/// </summary>
public static class HealthCheckServiceExtensions
{
    /// <summary>
    /// Adds database, LLM service, and Akahu API health checks to the health check system.
    /// Liveness checks (tag "live") verify the app process is running.
    /// Readiness checks (tag "ready") verify downstream dependencies are reachable.
    /// </summary>
    public static IServiceCollection AddHealthCheckServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured. " +
                "Ensure it is set in appsettings.json or environment variables.");

        services.AddHealthChecks()
            // EF Core connectivity check — fast, uses DbContext.Database.CanConnectAsync()
            .AddDbContextCheck<ApplicationDbContext>(
                name: "database-ef",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "ready" })
            // Npgsql raw connectivity check
            .AddNpgSql(
                connectionString,
                name: "database-npgsql",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "ready" })
            // Custom database health check (existing)
            .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" })
            .AddCheck<LlmServiceHealthCheck>("llm-service", tags: new[] { "ready" })
            .AddCheck<AkahuHealthCheck>("akahu", tags: new[] { "ready" });

        return services;
    }
}
