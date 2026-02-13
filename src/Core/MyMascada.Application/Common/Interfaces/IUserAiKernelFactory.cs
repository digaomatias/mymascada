using Microsoft.SemanticKernel;

namespace MyMascada.Application.Common.Interfaces;

public interface IUserAiKernelFactory
{
    Task<Kernel?> CreateKernelForUserAsync(Guid userId);
    Task<Kernel?> CreateChatKernelForUserAsync(Guid userId);
    Task<bool> IsAiAvailableForUserAsync(Guid userId);
    Task<AiConnectionTestResult> TestConnectionAsync(string providerType, string apiKey, string modelId, string? apiEndpoint = null);
}

public class AiConnectionTestResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int LatencyMs { get; set; }
    public string? ModelResponse { get; set; }
}
