using MyMascada.Application.Features.Authentication.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Strategy for completing user registration after the user entity is created.
/// Two implementations are registered conditionally at DI startup:
///   - EmailVerifiedRegistrationStrategy: sends verification email, requires email confirmation
///   - DirectRegistrationStrategy: skips email, issues JWT immediately
/// </summary>
public interface IRegistrationStrategy
{
    /// <summary>
    /// Whether new users should be created with EmailConfirmed = true.
    /// </summary>
    bool AutoConfirmEmail { get; }

    /// <summary>
    /// Completes registration after user creation (send verification email or issue tokens).
    /// </summary>
    Task<AuthenticationResponse> CompleteRegistrationAsync(
        User user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken);
}
