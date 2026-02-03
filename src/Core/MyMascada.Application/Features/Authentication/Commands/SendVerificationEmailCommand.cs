using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Email.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Authentication.Commands;

public class SendVerificationEmailCommand : IRequest<SendVerificationEmailResult>
{
    public Guid UserId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public class SendVerificationEmailResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public static SendVerificationEmailResult Succeeded() => new() { Success = true };
    public static SendVerificationEmailResult Failed(string error) => new() { Success = false, ErrorMessage = error };
}

public class SendVerificationEmailCommandHandler : IRequestHandler<SendVerificationEmailCommand, SendVerificationEmailResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailVerificationTokenRepository _tokenRepository;
    private readonly IEmailServiceFactory _emailServiceFactory;
    private readonly EmailVerificationOptions _options;
    private readonly AppOptions _appOptions;
    private readonly ILogger<SendVerificationEmailCommandHandler> _logger;

    // Security constants
    private const int TokenSizeBytes = 32;

    public SendVerificationEmailCommandHandler(
        IUserRepository userRepository,
        IEmailVerificationTokenRepository tokenRepository,
        IEmailServiceFactory emailServiceFactory,
        IOptions<EmailVerificationOptions> options,
        IOptions<AppOptions> appOptions,
        ILogger<SendVerificationEmailCommandHandler> logger)
    {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _emailServiceFactory = emailServiceFactory;
        _options = options.Value;
        _appOptions = appOptions.Value;
        _logger = logger;
    }

    public async Task<SendVerificationEmailResult> Handle(SendVerificationEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId);
        if (user == null)
        {
            _logger.LogWarning("Attempted to send verification email for non-existent user {UserId}", request.UserId);
            return SendVerificationEmailResult.Failed("User not found");
        }

        if (user.EmailConfirmed)
        {
            _logger.LogInformation("User {UserId} email already verified", request.UserId);
            return SendVerificationEmailResult.Failed("Email already verified");
        }

        // Check rate limit
        var recentRequestCount = await _tokenRepository.GetRecentRequestCountAsync(user.Id, TimeSpan.FromHours(1));
        if (recentRequestCount >= _options.MaxRequestsPerHour)
        {
            _logger.LogWarning("Email verification rate limit exceeded for user {UserId}", user.Id);
            return SendVerificationEmailResult.Failed("Too many verification requests. Please try again later.");
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
        var verificationToken = new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddHours(_options.TokenExpirationHours),
            IsUsed = false,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent
        };

        await _tokenRepository.AddAsync(verificationToken);

        // Build verification URL (use specific FrontendVerifyUrl if configured, otherwise derive from AppOptions.FrontendUrl)
        var frontendVerifyBaseUrl = string.IsNullOrEmpty(_options.FrontendVerifyUrl)
            ? $"{_appOptions.FrontendUrl}/auth/verify-email"
            : _options.FrontendVerifyUrl;
        var verifyUrl = $"{frontendVerifyBaseUrl}?token={Uri.EscapeDataString(rawToken)}&email={Uri.EscapeDataString(user.Email)}";

        // Send email
        try
        {
            var emailService = _emailServiceFactory.GetDefaultProvider();

            var emailMessage = new TemplatedEmailMessage
            {
                To = user.Email,
                ToName = user.FullName,
                TemplateName = "email-verification",
                TemplateData = new Dictionary<string, object>
                {
                    { "UserName", user.FirstName },
                    { "VerifyUrl", verifyUrl },
                    { "ExpirationHours", _options.TokenExpirationHours },
                    { "CurrentYear", DateTime.UtcNow.Year }
                },
                CorrelationId = verificationToken.Id.ToString()
            };

            var emailResult = await emailService.SendTemplateAsync(emailMessage, cancellationToken);

            if (!emailResult.Success)
            {
                _logger.LogError("Failed to send verification email to user {UserId}: {Error}",
                    user.Id, emailResult.ErrorMessage);
                return SendVerificationEmailResult.Failed("Failed to send verification email. Please try again later.");
            }

            _logger.LogInformation("Verification email sent to user {UserId}", user.Id);
            return SendVerificationEmailResult.Succeeded();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending verification email to user {UserId}", user.Id);
            return SendVerificationEmailResult.Failed("Failed to send verification email. Please try again later.");
        }
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
