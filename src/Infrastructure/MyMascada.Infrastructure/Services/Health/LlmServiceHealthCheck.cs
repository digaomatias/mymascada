using Microsoft.Extensions.Diagnostics.HealthChecks;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Infrastructure.Services.Health;

/// <summary>
/// Health check for the LLM categorization service (Claude API).
/// Reports degraded (not unhealthy) when the service is unavailable,
/// since the application can still function without AI categorization.
/// </summary>
public class LlmServiceHealthCheck : IHealthCheck
{
    private readonly ILlmCategorizationService _llmService;
    private readonly IApplicationLogger<LlmServiceHealthCheck> _logger;

    public LlmServiceHealthCheck(
        ILlmCategorizationService llmService,
        IApplicationLogger<LlmServiceHealthCheck> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isAvailable = await _llmService.IsServiceAvailableAsync(cancellationToken);

            if (isAvailable)
            {
                return HealthCheckResult.Healthy("LLM service (Claude API) is available");
            }

            return HealthCheckResult.Degraded("LLM service (Claude API) is not responding");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM service health check failed");
            return HealthCheckResult.Degraded("LLM service health check failed", ex);
        }
    }
}
