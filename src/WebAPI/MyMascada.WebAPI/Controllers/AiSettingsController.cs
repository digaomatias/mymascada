using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/ai-settings")]
[Authorize]
public class AiSettingsController : ControllerBase
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserAiSettingsRepository _repository;
    private readonly ISettingsEncryptionService _encryptionService;
    private readonly IUserAiKernelFactory _kernelFactory;

    public AiSettingsController(
        ICurrentUserService currentUserService,
        IUserAiSettingsRepository repository,
        ISettingsEncryptionService encryptionService,
        IUserAiKernelFactory kernelFactory)
    {
        _currentUserService = currentUserService;
        _repository = repository;
        _encryptionService = encryptionService;
        _kernelFactory = kernelFactory;
    }

    [HttpGet]
    public async Task<ActionResult<AiSettingsResponse>> GetSettings([FromQuery] string purpose = "general")
    {
        var userId = _currentUserService.GetUserId();
        var settings = await _repository.GetByUserIdAsync(userId, purpose);

        if (settings == null)
        {
            return Ok(new AiSettingsResponse { HasSettings = false });
        }

        return Ok(new AiSettingsResponse
        {
            HasSettings = true,
            ProviderType = settings.ProviderType,
            ProviderName = settings.ProviderName,
            ModelId = settings.ModelId,
            ApiEndpoint = settings.ApiEndpoint,
            HasApiKey = !string.IsNullOrEmpty(settings.EncryptedApiKey),
            ApiKeyLastFour = GetLastFour(settings.EncryptedApiKey),
            IsValidated = settings.IsValidated,
            LastValidatedAt = settings.LastValidatedAt
        });
    }

    [HttpPut]
    public async Task<ActionResult<AiSettingsResponse>> SaveSettings(
        [FromBody] SaveAiSettingsRequest request,
        [FromQuery] string purpose = "general")
    {
        if (string.IsNullOrWhiteSpace(request.ProviderType))
            return BadRequest(new { Error = "Provider type is required." });
        if (string.IsNullOrWhiteSpace(request.ProviderName))
            return BadRequest(new { Error = "Provider name is required." });
        if (string.IsNullOrWhiteSpace(request.ModelId))
            return BadRequest(new { Error = "Model ID is required." });

        // Validate API endpoint URL if provided
        if (!string.IsNullOrWhiteSpace(request.ApiEndpoint))
        {
            if (!Uri.TryCreate(request.ApiEndpoint, UriKind.Absolute, out var uri))
                return BadRequest(new { Error = "Invalid API endpoint URL format." });

            // HTTPS required except for localhost
            var isLocalhost = uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "::1";
            if (!isLocalhost && uri.Scheme != "https")
                return BadRequest(new { Error = "API endpoint must use HTTPS (except for localhost)." });
        }

        var userId = _currentUserService.GetUserId();
        var existing = await _repository.GetByUserIdAsync(userId, purpose);

        string? encryptedApiKey = existing?.EncryptedApiKey;
        if (!string.IsNullOrEmpty(request.ApiKey))
        {
            encryptedApiKey = _encryptionService.Encrypt(request.ApiKey);
        }

        if (existing == null)
        {
            var settings = new UserAiSettings
            {
                UserId = userId,
                Purpose = purpose,
                ProviderType = request.ProviderType,
                ProviderName = request.ProviderName,
                EncryptedApiKey = encryptedApiKey,
                ModelId = request.ModelId,
                ApiEndpoint = request.ApiEndpoint,
                IsValidated = false
            };

            await _repository.AddAsync(settings);
            existing = settings;
        }
        else
        {
            existing.ProviderType = request.ProviderType;
            existing.ProviderName = request.ProviderName;
            existing.EncryptedApiKey = encryptedApiKey;
            existing.ModelId = request.ModelId;
            existing.ApiEndpoint = request.ApiEndpoint;
            existing.IsValidated = false;
            existing.UpdatedAt = DateTimeProvider.UtcNow;

            await _repository.UpdateAsync(existing);
        }

        return Ok(new AiSettingsResponse
        {
            HasSettings = true,
            ProviderType = existing.ProviderType,
            ProviderName = existing.ProviderName,
            ModelId = existing.ModelId,
            ApiEndpoint = existing.ApiEndpoint,
            HasApiKey = !string.IsNullOrEmpty(existing.EncryptedApiKey),
            ApiKeyLastFour = GetLastFour(existing.EncryptedApiKey),
            IsValidated = existing.IsValidated,
            LastValidatedAt = existing.LastValidatedAt
        });
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteSettings([FromQuery] string purpose = "general")
    {
        var userId = _currentUserService.GetUserId();
        var settings = await _repository.GetByUserIdAsync(userId, purpose);

        if (settings == null)
        {
            return NotFound(new { Error = "No AI settings found." });
        }

        await _repository.DeleteAsync(settings);

        return Ok(new { Message = "AI settings removed." });
    }

    [HttpPost("test")]
    public async Task<ActionResult<AiConnectionTestResult>> TestConnection(
        [FromBody] TestConnectionRequest request,
        [FromQuery] string purpose = "general")
    {
        if (string.IsNullOrWhiteSpace(request.ProviderType))
            return BadRequest(new { Error = "Provider type is required." });
        if (string.IsNullOrWhiteSpace(request.ModelId))
            return BadRequest(new { Error = "Model ID is required." });

        // Resolve API key: use provided key, or fall back to existing encrypted key
        var apiKey = request.ApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            var userId = _currentUserService.GetUserId();
            var existing = await _repository.GetByUserIdAsync(userId, purpose);
            if (existing != null && !string.IsNullOrEmpty(existing.EncryptedApiKey))
            {
                apiKey = _encryptionService.Decrypt(existing.EncryptedApiKey);
            }
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            return BadRequest(new { Error = "API key is required for testing." });
        }

        // Validate endpoint URL if provided
        if (!string.IsNullOrWhiteSpace(request.ApiEndpoint))
        {
            if (!Uri.TryCreate(request.ApiEndpoint, UriKind.Absolute, out var uri))
                return BadRequest(new { Error = "Invalid API endpoint URL format." });

            var isLocalhost = uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "::1";
            if (!isLocalhost && uri.Scheme != "https")
                return BadRequest(new { Error = "API endpoint must use HTTPS (except for localhost)." });
        }

        var result = await _kernelFactory.TestConnectionAsync(
            request.ProviderType, apiKey, request.ModelId, request.ApiEndpoint);

        // If test succeeded and user has existing settings, mark as validated
        if (result.Success)
        {
            var userId = _currentUserService.GetUserId();
            var existing = await _repository.GetByUserIdAsync(userId, purpose);
            if (existing != null)
            {
                existing.IsValidated = true;
                existing.LastValidatedAt = DateTimeProvider.UtcNow;
                await _repository.UpdateAsync(existing);
            }
        }

        return Ok(result);
    }

    [HttpGet("providers")]
    public ActionResult<List<AiProviderPreset>> GetProviders()
    {
        var providers = new List<AiProviderPreset>
        {
            new()
            {
                Id = "openai",
                Name = "OpenAI",
                ProviderType = "openai",
                DefaultEndpoint = null,
                Models = new List<AiModelPreset>
                {
                    new() { Id = "gpt-4o-mini", Name = "GPT-4o Mini" },
                    new() { Id = "gpt-4o", Name = "GPT-4o" },
                    new() { Id = "gpt-4-turbo", Name = "GPT-4 Turbo" }
                }
            },
            new()
            {
                Id = "deepseek",
                Name = "DeepSeek",
                ProviderType = "openai-compatible",
                DefaultEndpoint = "https://api.deepseek.com",
                Models = new List<AiModelPreset>
                {
                    new() { Id = "deepseek-chat", Name = "DeepSeek Chat" },
                    new() { Id = "deepseek-reasoner", Name = "DeepSeek Reasoner" }
                }
            },
            new()
            {
                Id = "groq",
                Name = "Groq",
                ProviderType = "openai-compatible",
                DefaultEndpoint = "https://api.groq.com/openai",
                Models = new List<AiModelPreset>
                {
                    new() { Id = "llama-3.3-70b-versatile", Name = "Llama 3.3 70B" },
                    new() { Id = "mixtral-8x7b-32768", Name = "Mixtral 8x7B" }
                }
            },
            new()
            {
                Id = "ollama",
                Name = "Ollama (Local)",
                ProviderType = "openai-compatible",
                DefaultEndpoint = "http://localhost:11434",
                Models = new List<AiModelPreset>
                {
                    new() { Id = "llama3.2", Name = "Llama 3.2" },
                    new() { Id = "mistral", Name = "Mistral" },
                    new() { Id = "qwen2.5", Name = "Qwen 2.5" }
                }
            },
            new()
            {
                Id = "custom",
                Name = "Custom (OpenAI-compatible)",
                ProviderType = "openai-compatible",
                DefaultEndpoint = null,
                Models = new List<AiModelPreset>()
            }
        };

        return Ok(providers);
    }

    private string? GetLastFour(string? encryptedApiKey)
    {
        if (string.IsNullOrEmpty(encryptedApiKey))
            return null;

        try
        {
            var decrypted = _encryptionService.Decrypt(encryptedApiKey);
            return decrypted.Length >= 4 ? decrypted[^4..] : decrypted;
        }
        catch
        {
            return null;
        }
    }
}

public class AiSettingsResponse
{
    public bool HasSettings { get; set; }
    public string? ProviderType { get; set; }
    public string? ProviderName { get; set; }
    public string? ModelId { get; set; }
    public string? ApiEndpoint { get; set; }
    public bool HasApiKey { get; set; }
    public string? ApiKeyLastFour { get; set; }
    public bool IsValidated { get; set; }
    public DateTime? LastValidatedAt { get; set; }
}

public class SaveAiSettingsRequest
{
    public string ProviderType { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string? ApiEndpoint { get; set; }
}

public class TestConnectionRequest
{
    public string ProviderType { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string? ApiEndpoint { get; set; }
}

public class AiProviderPreset
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public string? DefaultEndpoint { get; set; }
    public List<AiModelPreset> Models { get; set; } = new();
}

public class AiModelPreset
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
