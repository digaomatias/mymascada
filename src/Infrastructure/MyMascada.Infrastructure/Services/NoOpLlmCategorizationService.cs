using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;

namespace MyMascada.Infrastructure.Services;

public class NoOpLlmCategorizationService : ILlmCategorizationService
{
    private readonly ILogger<NoOpLlmCategorizationService> _logger;

    public NoOpLlmCategorizationService(ILogger<NoOpLlmCategorizationService> logger)
    {
        _logger = logger;
        _logger.LogWarning("AI categorization is disabled. To enable, configure LLM:OpenAI:ApiKey.");
    }

    public Task<LlmCategorizationResponse> CategorizeTransactionsAsync(
        IEnumerable<Transaction> transactions,
        IEnumerable<Category> categories,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new LlmCategorizationResponse
        {
            Success = false,
            Errors = new List<string>
            {
                "AI categorization is not configured. Set the LLM:OpenAI:ApiKey configuration to enable AI features."
            }
        });
    }

    public Task<bool> IsServiceAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<string> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "AI categorization is not configured. Set the LLM:OpenAI:ApiKey configuration to enable AI features.");
    }
}
