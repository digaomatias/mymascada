namespace MyMascada.Application.Common.Configuration;

/// <summary>
/// Configuration options for password reset functionality
/// </summary>
public class PasswordResetOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "PasswordReset";

    /// <summary>
    /// Token expiration time in minutes (default: 30)
    /// </summary>
    public int TokenExpirationMinutes { get; set; } = 30;

    /// <summary>
    /// Maximum password reset requests per hour per email (default: 3)
    /// </summary>
    public int MaxRequestsPerHour { get; set; } = 3;

    /// <summary>
    /// Frontend URL for the reset password page
    /// </summary>
    public string FrontendResetUrl { get; set; } = "";
}
