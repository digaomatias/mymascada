namespace MyMascada.Application.Features.Authentication.DTOs;

public class AuthenticationResponse
{
    public bool IsSuccess { get; set; }
    public string? Token { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public UserDto? User { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? RefreshToken { get; set; } // Used internally, not exposed to client
    public DateTime? RefreshTokenExpiresAt { get; set; }

    /// <summary>
    /// Indicates that the user needs to verify their email address before they can login
    /// </summary>
    public bool RequiresEmailVerification { get; set; }

    /// <summary>
    /// Message to display to the user (e.g., "Please check your email to verify your account")
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Indicates that the account has been locked out due to too many failed login attempts.
    /// The client should show a user-friendly lockout message without revealing the exact unlock time.
    /// </summary>
    public bool IsAccountLocked { get; set; }
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public string Locale { get; set; } = "en";
    public string? ProfilePictureUrl { get; set; }
    public bool AiDescriptionCleaning { get; set; }
    public bool HasAiConfigured { get; set; }
    public bool IsOnboardingComplete { get; set; }

    /// <summary>
    /// Current subscription tier ("Free", "Pro", "Family", "SelfHosted").
    /// Used by the frontend to show/hide upsell banners and gated features.
    /// </summary>
    public string SubscriptionTier { get; set; } = "Free";

    /// <summary>
    /// True when the deployment is self-hosted (IFeatureFlags.StripeBilling == false).
    /// Self-hosted users never see upsell banners — they already have unlimited access.
    /// </summary>
    public bool IsSelfHosted { get; set; }
}