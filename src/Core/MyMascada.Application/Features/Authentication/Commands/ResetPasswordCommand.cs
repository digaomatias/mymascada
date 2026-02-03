using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Authentication.DTOs;

namespace MyMascada.Application.Features.Authentication.Commands;

public class ResetPasswordCommand : IRequest<PasswordResetResponse>
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
}

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, PasswordResetResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAuthenticationService _authenticationService;
    private readonly IValidator<ResetPasswordCommand> _validator;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        IUserRepository userRepository,
        IPasswordResetTokenRepository tokenRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IAuthenticationService authenticationService,
        IValidator<ResetPasswordCommand> validator,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _authenticationService = authenticationService;
        _validator = validator;
        _logger = logger;
    }

    public async Task<PasswordResetResponse> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        // Validate the request
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return PasswordResetResponse.Failure(validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        // Hash the provided token to compare with stored hash
        var tokenHash = HashToken(request.Token);

        // Look up the token
        var resetToken = await _tokenRepository.GetByTokenHashAsync(tokenHash);
        if (resetToken == null)
        {
            _logger.LogWarning("Password reset attempted with invalid token");
            return PasswordResetResponse.Failure("Invalid or expired password reset link. Please request a new one.");
        }

        // Check if token is already used
        if (resetToken.IsUsed)
        {
            _logger.LogWarning("Password reset attempted with already used token for user {UserId}", resetToken.UserId);
            return PasswordResetResponse.Failure("This password reset link has already been used. Please request a new one.");
        }

        // Check if token is expired
        if (resetToken.IsExpired)
        {
            _logger.LogWarning("Password reset attempted with expired token for user {UserId}", resetToken.UserId);
            return PasswordResetResponse.Failure("This password reset link has expired. Please request a new one.");
        }

        // Verify the email matches the token's user
        var user = await _userRepository.GetByIdAsync(resetToken.UserId);
        if (user == null || !user.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Password reset email mismatch for token");
            return PasswordResetResponse.Failure("Invalid or expired password reset link. Please request a new one.");
        }

        // Mark the token as used immediately (before password update to prevent replay)
        resetToken.MarkAsUsed();
        await _tokenRepository.UpdateAsync(resetToken);

        // Update the user's password
        var newPasswordHash = await _authenticationService.HashPasswordAsync(request.NewPassword);
        user.PasswordHash = newPasswordHash;
        user.SecurityStamp = _authenticationService.GenerateSecurityStamp();
        await _userRepository.UpdateAsync(user);

        // Revoke all refresh tokens for security (force re-login on all devices)
        var ipAddress = request.IpAddress ?? "unknown";
        await _refreshTokenRepository.RevokeAllUserTokensAsync(user.Id, ipAddress);

        // Invalidate any other password reset tokens for this user
        await _tokenRepository.InvalidateAllForUserAsync(user.Id);

        _logger.LogInformation("Password successfully reset for user {UserId}", user.Id);

        return PasswordResetResponse.Success("Your password has been reset successfully. You can now sign in with your new password.");
    }

    /// <summary>
    /// Computes SHA-256 hash of the token
    /// </summary>
    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
