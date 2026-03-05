namespace MyMascada.Application.Features.Authentication.DTOs;

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class RevokeTokenRequest
{
    public string? RefreshToken { get; set; }
}
