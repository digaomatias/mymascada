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
}