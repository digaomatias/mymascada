using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using MyMascada.Application.Common.Interfaces;
using System.Diagnostics;

namespace MyMascada.Infrastructure.Services.AI;

public class UserAiKernelFactory : IUserAiKernelFactory
{
    private readonly IUserAiSettingsRepository _settingsRepository;
    private readonly ISettingsEncryptionService _encryptionService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserAiKernelFactory> _logger;

    public UserAiKernelFactory(
        IUserAiSettingsRepository settingsRepository,
        ISettingsEncryptionService encryptionService,
        IConfiguration configuration,
        ILogger<UserAiKernelFactory> logger)
    {
        _settingsRepository = settingsRepository;
        _encryptionService = encryptionService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Kernel?> CreateKernelForUserAsync(Guid userId)
    {
        var settings = await _settingsRepository.GetByUserIdAsync(userId);

        if (settings != null && !string.IsNullOrEmpty(settings.EncryptedApiKey))
        {
            try
            {
                var apiKey = _encryptionService.Decrypt(settings.EncryptedApiKey);
                return BuildKernel(settings.ProviderType, apiKey, settings.ModelId, settings.ApiEndpoint);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create kernel from user AI settings for user {UserId}, falling back to global config", userId);
            }
        }

        // Fallback to global configuration
        var globalApiKey = _configuration["LLM:OpenAI:ApiKey"];
        var globalModel = _configuration["LLM:OpenAI:Model"] ?? "gpt-4o-mini";

        if (!string.IsNullOrEmpty(globalApiKey) && globalApiKey != "YOUR_OPENAI_API_KEY")
        {
            return BuildKernel("openai", globalApiKey, globalModel, null);
        }

        return null;
    }

    public async Task<bool> IsAiAvailableForUserAsync(Guid userId)
    {
        var settings = await _settingsRepository.GetByUserIdAsync(userId);
        if (settings != null && !string.IsNullOrEmpty(settings.EncryptedApiKey))
        {
            return true;
        }

        var globalApiKey = _configuration["LLM:OpenAI:ApiKey"];
        return !string.IsNullOrEmpty(globalApiKey) && globalApiKey != "YOUR_OPENAI_API_KEY";
    }

    public async Task<AiConnectionTestResult> TestConnectionAsync(
        string providerType, string apiKey, string modelId, string? apiEndpoint = null)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var kernel = BuildKernel(providerType, apiKey, modelId, apiEndpoint);
            var response = await kernel.InvokePromptAsync("Say hello in one word.");
            sw.Stop();

            return new AiConnectionTestResult
            {
                Success = true,
                LatencyMs = (int)sw.ElapsedMilliseconds,
                ModelResponse = response.ToString()
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "AI connection test failed for provider {ProviderType}, model {ModelId}", providerType, modelId);

            return new AiConnectionTestResult
            {
                Success = false,
                Error = ex.Message,
                LatencyMs = (int)sw.ElapsedMilliseconds
            };
        }
    }

    private static Kernel BuildKernel(string providerType, string apiKey, string modelId, string? apiEndpoint)
    {
        var builder = Kernel.CreateBuilder();

        if (providerType == "openai-compatible" && !string.IsNullOrEmpty(apiEndpoint))
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(apiEndpoint),
                Timeout = TimeSpan.FromMinutes(5)
            };
            builder.AddOpenAIChatCompletion(modelId, apiKey, httpClient: httpClient);
        }
        else
        {
            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            builder.AddOpenAIChatCompletion(modelId, apiKey, httpClient: httpClient);
        }

        return builder.Build();
    }
}
