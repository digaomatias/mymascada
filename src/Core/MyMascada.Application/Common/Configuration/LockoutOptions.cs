namespace MyMascada.Application.Common.Configuration;

/// <summary>
/// Configuration options for account lockout after repeated failed login attempts.
/// </summary>
public class LockoutOptions
{
    public const string SectionName = "Lockout";

    /// <summary>
    /// Number of failed login attempts before the account is locked.
    /// Defaults to 5, matching ASP.NET Core Identity defaults.
    /// </summary>
    public int MaxFailedAccessAttempts { get; set; } = 5;

    /// <summary>
    /// Duration (in minutes) for which the account remains locked.
    /// Defaults to 15 minutes.
    /// </summary>
    public int DefaultLockoutTimeSpanMinutes { get; set; } = 15;

    /// <summary>
    /// Whether lockout is enabled for new users.
    /// Defaults to true, matching ASP.NET Core Identity defaults.
    /// </summary>
    public bool LockoutAllowedForNewUsers { get; set; } = true;
}
