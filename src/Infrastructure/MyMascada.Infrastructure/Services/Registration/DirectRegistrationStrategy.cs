using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Authentication.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Infrastructure.Services.Registration;

/// <summary>
/// Registration strategy used when email is NOT configured (self-hosted).
/// Skips email verification and issues JWT tokens immediately.
/// </summary>
public class DirectRegistrationStrategy : IRegistrationStrategy
{
    private readonly ITokenService _tokenService;
    private readonly ILogger<DirectRegistrationStrategy> _logger;

    public bool AutoConfirmEmail => true;

    public DirectRegistrationStrategy(
        ITokenService tokenService,
        ILogger<DirectRegistrationStrategy> logger)
    {
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<AuthenticationResponse> CompleteRegistrationAsync(
        User user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Email not configured — issuing JWT directly for user {UserId}", user.Id);

        var token = await _tokenService.GenerateJwtTokenAsync(user);
        var refreshTokenResult = await _tokenService.GenerateRefreshTokenAsync(user, ipAddress ?? "0.0.0.0");

        return new AuthenticationResponse
        {
            IsSuccess = true,
            RequiresEmailVerification = false,
            Token = token,
            RefreshToken = refreshTokenResult.RawToken,
            RefreshTokenExpiresAt = refreshTokenResult.ExpiresAt,
            Message = "Registration successful!",
            User = MapUser(user)
        };
    }

    private static UserDto MapUser(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        UserName = user.UserName,
        FirstName = user.FirstName,
        LastName = user.LastName,
        FullName = user.FullName,
        Currency = user.Currency,
        TimeZone = user.TimeZone,
        ProfilePictureUrl = user.ProfilePictureUrl
    };
}
