using Microsoft.Extensions.Logging;

namespace MyMascada.Infrastructure.Services.AI;

/// <summary>
/// Extracts token usage information from Semantic Kernel response metadata.
/// Semantic Kernel stores OpenAI usage data in Metadata["Usage"] as a CompletionsUsage object.
/// </summary>
public static class SemanticKernelTokenExtractor
{
    public static (int promptTokens, int completionTokens, int totalTokens) ExtractTokenUsage(
        IReadOnlyDictionary<string, object?>? metadata, ILogger? logger = null)
    {
        if (metadata == null)
            return (0, 0, 0);

        try
        {
            if (!metadata.TryGetValue("Usage", out var usageObj) || usageObj == null)
                return (0, 0, 0);

            // Semantic Kernel exposes usage as dynamic objects from different OpenAI SDK versions.
            // Use reflection to extract the token counts robustly.
            var usageType = usageObj.GetType();

            var promptTokens = TryGetIntProperty(usageType, usageObj, "InputTokenCount")
                ?? TryGetIntProperty(usageType, usageObj, "PromptTokens")
                ?? 0;

            var completionTokens = TryGetIntProperty(usageType, usageObj, "OutputTokenCount")
                ?? TryGetIntProperty(usageType, usageObj, "CompletionTokens")
                ?? 0;

            var totalTokens = TryGetIntProperty(usageType, usageObj, "TotalTokenCount")
                ?? TryGetIntProperty(usageType, usageObj, "TotalTokens")
                ?? (promptTokens + completionTokens);

            return (promptTokens, completionTokens, totalTokens);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to extract token usage from metadata");
            return (0, 0, 0);
        }
    }

    public static string ExtractModelId(IReadOnlyDictionary<string, object?>? metadata)
    {
        if (metadata == null)
            return "unknown";

        if (metadata.TryGetValue("ModelId", out var modelObj) && modelObj is string modelId && !string.IsNullOrEmpty(modelId))
            return modelId;

        return "unknown";
    }

    private static int? TryGetIntProperty(Type type, object obj, string propertyName)
    {
        var prop = type.GetProperty(propertyName);
        if (prop == null) return null;

        var value = prop.GetValue(obj);
        if (value == null) return null;

        return Convert.ToInt32(value);
    }
}
