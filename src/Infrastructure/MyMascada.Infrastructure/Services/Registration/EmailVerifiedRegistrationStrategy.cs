using MediatR;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Authentication.Commands;
using MyMascada.Application.Features.Authentication.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Infrastructure.Services.Registration;

/// <summary>
/// Registration strategy used when email IS configured.
/// Sends a verification email and requires the user to confirm before login.
/// </summary>
public class EmailVerifiedRegistrationStrategy : IRegistrationStrategy
{
    private readonly IMediator _mediator;
    private readonly ILogger<EmailVerifiedRegistrationStrategy> _logger;

    public bool AutoConfirmEmail => false;

    public EmailVerifiedRegistrationStrategy(
        IMediator mediator,
        ILogger<EmailVerifiedRegistrationStrategy> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<AuthenticationResponse> CompleteRegistrationAsync(
        User user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var verificationResult = await _mediator.Send(new SendVerificationEmailCommand
        {
            UserId = user.Id,
            IpAddress = ipAddress,
            UserAgent = userAgent
        }, cancellationToken);

        if (!verificationResult.Success)
        {
            _logger.LogWarning("Failed to send verification email for user {UserId}: {Error}",
                user.Id, verificationResult.ErrorMessage);
        }

        return new AuthenticationResponse
        {
            IsSuccess = true,
            RequiresEmailVerification = true,
            Message = "Registration successful! Please check your email to verify your account.",
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
