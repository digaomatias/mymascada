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
}
