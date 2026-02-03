namespace MyMascada.Application.Features.Authentication.DTOs;

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Currency { get; set; } = "USD";
    public string TimeZone { get; set; } = "UTC";
    public string? InviteCode { get; set; }
}