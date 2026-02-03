namespace MyMascada.Application.Features.Authentication.DTOs;

/// <summary>
/// Request to initiate password reset (forgot password)
/// </summary>
public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Request to reset password with token
/// </summary>
public class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>
/// Response for password reset operations
/// </summary>
public class PasswordResetResponse
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();

    public static PasswordResetResponse Success(string message = "")
    {
        return new PasswordResetResponse
        {
            IsSuccess = true,
            Message = message
        };
    }

    public static PasswordResetResponse Failure(params string[] errors)
    {
        return new PasswordResetResponse
        {
            IsSuccess = false,
            Errors = errors.ToList()
        };
    }
}

/// <summary>
/// Request to change password (authenticated user)
/// </summary>
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
