namespace MyMascada.Application.Common.Configuration;

/// <summary>
/// Configuration options for email verification functionality
/// </summary>
public class EmailVerificationOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "EmailVerification";

    /// <summary>
    /// Token expiration time in hours (default: 24)
    /// </summary>
    public int TokenExpirationHours { get; set; } = 24;

    /// <summary>
    /// Maximum verification email requests per hour per user (default: 3)
    /// </summary>
    public int MaxRequestsPerHour { get; set; } = 3;

    /// <summary>
    /// Frontend URL for the verify email page
    /// </summary>
    public string FrontendVerifyUrl { get; set; } = "";
}
