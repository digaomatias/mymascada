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
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<DirectRegistrationStrategy> _logger;

    public bool AutoConfirmEmail => true;

    public DirectRegistrationStrategy(
        IAuthenticationService authenticationService,
        ILogger<DirectRegistrationStrategy> logger)
    {
        _authenticationService = authenticationService;
        _logger = logger;
    }

    public async Task<AuthenticationResponse> CompleteRegistrationAsync(
        User user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Email not configured — issuing JWT directly for user {UserId}", user.Id);

        var token = await _authenticationService.GenerateJwtTokenAsync(user);
        var refreshToken = await _authenticationService.GenerateRefreshTokenAsync(user, ipAddress ?? "0.0.0.0");

        return new AuthenticationResponse
        {
            IsSuccess = true,
            RequiresEmailVerification = false,
            Token = token,
            RefreshToken = refreshToken.Token,
            RefreshTokenExpiresAt = refreshToken.ExpiryDate,
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
