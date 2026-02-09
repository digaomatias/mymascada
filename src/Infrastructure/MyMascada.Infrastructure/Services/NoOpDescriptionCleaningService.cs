using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Infrastructure.Services;

public class NoOpDescriptionCleaningService : IDescriptionCleaningService
{
    private readonly ILogger<NoOpDescriptionCleaningService> _logger;

    public NoOpDescriptionCleaningService(ILogger<NoOpDescriptionCleaningService> logger)
    {
        _logger = logger;
        _logger.LogWarning("AI description cleaning is disabled. To enable, configure LLM:OpenAI:ApiKey.");
    }

    public Task<DescriptionCleaningResponse> CleanDescriptionsAsync(
        IEnumerable<DescriptionCleaningInput> descriptions,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DescriptionCleaningResponse
        {
            Success = false,
            Errors = new List<string>
            {
                "AI description cleaning is not configured. Set the LLM:OpenAI:ApiKey configuration to enable AI features."
            }
        });
    }

    public Task<bool> IsServiceAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}
