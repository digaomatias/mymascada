using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Authentication.DTOs;

namespace MyMascada.Application.Features.Authentication.Commands;

public class ChangePasswordCommand : IRequest<PasswordResetResponse>
{
    public Guid UserId { get; set; }
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
}

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, PasswordResetResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAuthenticationService _authenticationService;
    private readonly IValidator<ChangePasswordCommand> _validator;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public ChangePasswordCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IAuthenticationService authenticationService,
        IValidator<ChangePasswordCommand> validator,
        ILogger<ChangePasswordCommandHandler> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _authenticationService = authenticationService;
        _validator = validator;
        _logger = logger;
    }

    public async Task<PasswordResetResponse> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return PasswordResetResponse.Failure(validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var user = await _userRepository.GetByIdAsync(request.UserId);
        if (user == null)
        {
            return PasswordResetResponse.Failure("User not found.");
        }

        // Verify current password
        var isCurrentPasswordValid = await _authenticationService.VerifyPasswordAsync(request.CurrentPassword, user.PasswordHash);
        if (!isCurrentPasswordValid)
        {
            _logger.LogWarning("Password change attempted with incorrect current password for user {UserId}", request.UserId);
            return PasswordResetResponse.Failure("Current password is incorrect.");
        }

        // Update password and security stamp
        user.PasswordHash = await _authenticationService.HashPasswordAsync(request.NewPassword);
        user.SecurityStamp = _authenticationService.GenerateSecurityStamp();
        await _userRepository.UpdateAsync(user);

        // Revoke all refresh tokens (force re-login on all devices)
        var ipAddress = request.IpAddress ?? "unknown";
        await _refreshTokenRepository.RevokeAllUserTokensAsync(user.Id, ipAddress);

        _logger.LogInformation("Password successfully changed for user {UserId}. All sessions invalidated.", user.Id);

        return PasswordResetResponse.Success("Your password has been changed successfully. Please sign in again.");
    }
}
