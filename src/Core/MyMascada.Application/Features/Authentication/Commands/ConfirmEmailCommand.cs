using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.Authentication.Commands;

public class ConfirmEmailCommand : IRequest<ConfirmEmailResult>
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

public class ConfirmEmailResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public static ConfirmEmailResult Succeeded(string message) => new() { Success = true, Message = message };
    public static ConfirmEmailResult Failed(string message) => new() { Success = false, Message = message };
}

public class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Verification token is required");
    }
}

public class ConfirmEmailCommandHandler : IRequestHandler<ConfirmEmailCommand, ConfirmEmailResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailVerificationTokenRepository _tokenRepository;
    private readonly ILogger<ConfirmEmailCommandHandler> _logger;

    public ConfirmEmailCommandHandler(
        IUserRepository userRepository,
        IEmailVerificationTokenRepository tokenRepository,
        ILogger<ConfirmEmailCommandHandler> logger)
    {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _logger = logger;
    }

    public async Task<ConfirmEmailResult> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
    {
        // Hash the provided token to compare with stored hash
        var tokenHash = HashToken(request.Token);

        // Look up the token
        var verificationToken = await _tokenRepository.GetByTokenHashAsync(tokenHash);
        if (verificationToken == null)
        {
            _logger.LogWarning("Email verification attempted with invalid token");
            return ConfirmEmailResult.Failed("Invalid or expired verification link. Please request a new one.");
        }

        // Check if token is already used
        if (verificationToken.IsUsed)
        {
            _logger.LogWarning("Email verification attempted with already used token for user {UserId}", verificationToken.UserId);
            return ConfirmEmailResult.Failed("This verification link has already been used.");
        }

        // Check if token is expired
        if (verificationToken.IsExpired)
        {
            _logger.LogWarning("Email verification attempted with expired token for user {UserId}", verificationToken.UserId);
            return ConfirmEmailResult.Failed("This verification link has expired. Please request a new one.");
        }

        // Verify the email matches the token's user
        var user = await _userRepository.GetByIdAsync(verificationToken.UserId);
        if (user == null || !user.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Email verification email mismatch for token");
            return ConfirmEmailResult.Failed("Invalid or expired verification link. Please request a new one.");
        }

        // Check if already verified
        if (user.EmailConfirmed)
        {
            _logger.LogInformation("Email verification attempted for already verified user {UserId}", user.Id);
            return ConfirmEmailResult.Succeeded("Your email is already verified. You can now sign in.");
        }

        // Mark the token as used
        verificationToken.MarkAsUsed();
        await _tokenRepository.UpdateAsync(verificationToken);

        // Mark the user's email as confirmed
        user.EmailConfirmed = true;
        await _userRepository.UpdateAsync(user);

        // Invalidate any other verification tokens for this user
        await _tokenRepository.InvalidateAllForUserAsync(user.Id);

        _logger.LogInformation("Email successfully verified for user {UserId}", user.Id);

        return ConfirmEmailResult.Succeeded("Your email has been verified successfully. You can now sign in.");
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
