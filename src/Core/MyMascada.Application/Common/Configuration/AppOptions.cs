namespace MyMascada.Application.Common.Configuration;

/// <summary>
/// Core application configuration options.
/// </summary>
public class AppOptions
{
    public const string SectionName = "App";

    /// <summary>
    /// The base URL of the frontend application (e.g., http://localhost:3000 or https://finance.example.com).
    /// Used to construct callback URLs for email verification, password reset, OAuth, etc.
    /// </summary>
    public string FrontendUrl { get; set; } = "http://localhost:3000";

    /// <summary>
    /// The public base URL of the API (e.g., https://api.mymascada.com).
    /// Used for OAuth redirect URIs and other external-facing URLs.
    /// When empty, falls back to Request.Host (works for direct access but not behind rewrites).
    /// </summary>
    public string ApiBaseUrl { get; set; } = "";
}
