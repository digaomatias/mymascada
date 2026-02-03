using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.Services.BankIntegration.Providers;

namespace MyMascada.Infrastructure.Services.Health;

/// <summary>
/// Health check for the Akahu bank integration API.
/// Performs a simple HTTP connectivity check to verify the Akahu API is reachable.
/// Reports degraded (not unhealthy) when the service is unavailable,
/// since the application can still function without bank sync.
/// </summary>
public class AkahuHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApplicationLogger<AkahuHealthCheck> _logger;
    private readonly AkahuOptions _options;

    public AkahuHealthCheck(
        IHttpClientFactory httpClientFactory,
        IApplicationLogger<AkahuHealthCheck> logger,
        IOptions<AkahuOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return HealthCheckResult.Degraded("Akahu is not enabled");
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var httpClient = _httpClientFactory.CreateClient("AkahuHealthCheck");
            var response = await httpClient.GetAsync(_options.ApiBaseUrl, cts.Token);

            // Any response (even 401/403) means the service is reachable
            return HealthCheckResult.Healthy(
                $"Akahu API is reachable (status: {(int)response.StatusCode})");
        }
        catch (TaskCanceledException)
        {
            return HealthCheckResult.Degraded("Akahu API health check timed out");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Akahu health check failed");
            return HealthCheckResult.Degraded("Akahu API is not reachable", ex);
        }
    }
}
