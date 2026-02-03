namespace MyMascada.Application.Features.Authentication.DTOs;

public class LoginRequest
{
    public string EmailOrUserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
}