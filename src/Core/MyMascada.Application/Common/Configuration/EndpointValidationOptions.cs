namespace MyMascada.Application.Common.Configuration;

/// <summary>
/// Configuration options for SSRF endpoint validation.
/// Bound from the "EndpointValidation" section in appsettings.json.
/// </summary>
public class EndpointValidationOptions
{
    public const string SectionName = "EndpointValidation";

    /// <summary>
    /// Known AI provider hostnames that are always allowed (bypass DNS resolution check).
    /// Configured via appsettings.json under <c>EndpointValidation:AllowedAiProviderHosts</c>.
    /// <para>
    /// <b>Important:</b> ASP.NET Core configuration replaces array elements by index, not by merging.
    /// To override this list in appsettings.Production.json, you must provide the complete list —
    /// partial overrides will only replace elements at matching indices, leaving defaults at other positions.
    /// </para>
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
