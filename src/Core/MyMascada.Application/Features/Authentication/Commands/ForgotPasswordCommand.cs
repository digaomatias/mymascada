using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Authentication.DTOs;
using MyMascada.Application.Features.Email.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Authentication.Commands;

public class ForgotPasswordCommand : IRequest<PasswordResetResponse>
{
    public string Email { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, PasswordResetResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IEmailServiceFactory _emailServiceFactory;
    private readonly IValidator<ForgotPasswordCommand> _validator;
    private readonly PasswordResetOptions _options;
    private readonly AppOptions _appOptions;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    // Security constants
    private const int TokenSizeBytes = 32;

    public ForgotPasswordCommandHandler(
        IUserRepository userRepository,
        IPasswordResetTokenRepository tokenRepository,
        IEmailServiceFactory emailServiceFactory,
        IValidator<ForgotPasswordCommand> validator,
        IOptions<PasswordResetOptions> options,
        IOptions<AppOptions> appOptions,
        ILogger<ForgotPasswordCommandHandler> logger)
    {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _emailServiceFactory = emailServiceFactory;
        _validator = validator;
        _options = options.Value;
        _appOptions = appOptions.Value;
        _logger = logger;
    }

    public async Task<PasswordResetResponse> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        // Always return the same generic message to prevent user enumeration
        const string genericMessage = "If your email address is registered with us, you will receive a password reset link shortly.";

        // Validate the request
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            // Still return generic message even for validation failures
            _logger.LogWarning("Password reset validation failed for email format");
            return PasswordResetResponse.Success(genericMessage);
        }

        var normalizedEmail = request.Email.ToUpperInvariant();

        // Look up user - if not found, return success anyway (no user enumeration)
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogInformation("Password reset requested for non-existent email");
            return PasswordResetResponse.Success(genericMessage);
        }

        // Check rate limit - still return generic message if exceeded
        var recentRequestCount = await _tokenRepository.GetRecentRequestCountAsync(user.Id, TimeSpan.FromHours(1));
        if (recentRequestCount >= _options.MaxRequestsPerHour)
        {
            _logger.LogWarning("Password reset rate limit exceeded for user {UserId}", user.Id);
            return PasswordResetResponse.Success(genericMessage);
        }

        // Invalidate any existing tokens for this user
        await _tokenRepository.InvalidateAllForUserAsync(user.Id);

        // Generate cryptographically secure token
        var tokenBytes = RandomNumberGenerator.GetBytes(TokenSizeBytes);
        var rawToken = Convert.ToBase64String(tokenBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('='); // Base64Url encoding

        // Hash the token for storage (never store plaintext)
        var tokenHash = HashToken(rawToken);

        // Create and save the token entity
        var tokenExpiration = _options.TokenExpirationMinutes;
        var passwordResetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(tokenExpiration),
            IsUsed = false,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent
        };

        await _tokenRepository.AddAsync(passwordResetToken);

        // Build reset URL (use specific FrontendResetUrl if configured, otherwise derive from AppOptions.FrontendUrl)
        var frontendBaseUrl = string.IsNullOrEmpty(_options.FrontendResetUrl)
            ? $"{_appOptions.FrontendUrl}/auth/reset-password"
            : _options.FrontendResetUrl;
        var resetUrl = $"{frontendBaseUrl}?token={Uri.EscapeDataString(rawToken)}&email={Uri.EscapeDataString(request.Email)}";

        // Send email (do not log the token!)
        try
        {
            var emailService = _emailServiceFactory.GetDefaultProvider();

            var emailMessage = new TemplatedEmailMessage
            {
                To = user.Email,
                ToName = user.FullName,
                TemplateName = "password-reset",
                TemplateData = new Dictionary<string, object>
                {
                    { "UserName", user.FirstName },
                    { "ResetUrl", resetUrl },
                    { "ExpirationMinutes", tokenExpiration },
                    { "CurrentYear", DateTime.UtcNow.Year }
                },
                CorrelationId = passwordResetToken.Id.ToString()
            };

            var emailResult = await emailService.SendTemplateAsync(emailMessage, cancellationToken);

            if (!emailResult.Success)
            {
                _logger.LogError("Failed to send password reset email to user {UserId}: {Error}",
                    user.Id, emailResult.ErrorMessage);
                // Still return success to not leak information
            }
            else
            {
                _logger.LogInformation("Password reset email sent to user {UserId}", user.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending password reset email to user {UserId}", user.Id);
            // Still return success to not leak information
        }

        return PasswordResetResponse.Success(genericMessage);
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
