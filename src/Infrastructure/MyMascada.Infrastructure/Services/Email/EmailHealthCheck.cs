using Microsoft.Extensions.Diagnostics.HealthChecks;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Infrastructure.Services.Email;

/// <summary>
/// Health check for email service availability.
/// Checks if the configured email provider is reachable.
/// </summary>
public class EmailHealthCheck : IHealthCheck
{
    private readonly IEmailServiceFactory _factory;
    private readonly IApplicationLogger<EmailHealthCheck> _logger;

    public EmailHealthCheck(
        IEmailServiceFactory factory,
        IApplicationLogger<EmailHealthCheck> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var provider = _factory.GetDefaultProvider();
            var isHealthy = await provider.IsHealthyAsync(cancellationToken);

            if (isHealthy)
            {
                return HealthCheckResult.Healthy(
                    $"Email provider '{provider.ProviderId}' ({provider.DisplayName}) is reachable");
            }

            return HealthCheckResult.Degraded(
                $"Email provider '{provider.ProviderId}' ({provider.DisplayName}) health check failed");
        }
        catch (InvalidOperationException ex)
        {
            // Provider not configured
            _logger.LogWarning(ex, "Email health check failed: provider not configured");
            return HealthCheckResult.Degraded("Email provider not configured", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email health check failed with exception");
            return HealthCheckResult.Unhealthy("Email service unavailable", ex);
        }
    }
}
