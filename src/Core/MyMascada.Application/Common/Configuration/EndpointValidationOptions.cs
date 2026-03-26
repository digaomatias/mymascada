namespace MyMascada.Application.Common.Configuration;

/// <summary>
/// Configuration options for SSRF endpoint validation.
/// </summary>
public class EndpointValidationOptions
{
    public const string SectionName = "EndpointValidation";

    /// <summary>
    /// Known AI provider hostnames that are always allowed (bypass DNS resolution check).
    /// </summary>
    public List<string> AllowedAiProviderHosts { get; set; } =
    [
        "api.openai.com",
        "api.anthropic.com",
        "api.deepseek.com",
        "api.groq.com",
        "generativelanguage.googleapis.com",
        "api.mistral.ai",
        "api.cohere.com",
        "api.together.xyz",
        "openrouter.ai",
    ];
}
