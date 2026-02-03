using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MyMascada.Application.Features.Authentication.Commands;

public class ResendVerificationEmailCommand : IRequest<ResendVerificationEmailResult>
{
    public string Email { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public class ResendVerificationEmailResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public static ResendVerificationEmailResult Succeeded(string message) => new() { Success = true, Message = message };
    public static ResendVerificationEmailResult Failed(string message) => new() { Success = false, Message = message };
}

public class ResendVerificationEmailCommandValidator : AbstractValidator<ResendVerificationEmailCommand>
{
    public ResendVerificationEmailCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");
    }
}

public class ResendVerificationEmailCommandHandler : IRequestHandler<ResendVerificationEmailCommand, ResendVerificationEmailResult>
{
    private readonly IMediator _mediator;
    private readonly Common.Interfaces.IUserRepository _userRepository;
    private readonly ILogger<ResendVerificationEmailCommandHandler> _logger;

    // Generic message to prevent user enumeration
    private const string GenericMessage = "If an unverified account exists with this email, a verification link has been sent.";

    public ResendVerificationEmailCommandHandler(
        IMediator mediator,
        Common.Interfaces.IUserRepository userRepository,
        ILogger<ResendVerificationEmailCommandHandler> logger)
    {
        _mediator = mediator;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<ResendVerificationEmailResult> Handle(ResendVerificationEmailCommand request, CancellationToken cancellationToken)
    {
        // Look up user - if not found, return success anyway (no user enumeration)
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogInformation("Resend verification requested for non-existent email");
            return ResendVerificationEmailResult.Succeeded(GenericMessage);
        }

        // If already verified, return success with generic message (no user enumeration)
        if (user.EmailConfirmed)
        {
            _logger.LogInformation("Resend verification requested for already verified user {UserId}", user.Id);
            return ResendVerificationEmailResult.Succeeded(GenericMessage);
        }

        // Send verification email using the existing command
        var sendResult = await _mediator.Send(new SendVerificationEmailCommand
        {
            UserId = user.Id,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent
        }, cancellationToken);

        if (!sendResult.Success)
        {
            // If it's a rate limit error, we can return a more specific message
            if (sendResult.ErrorMessage?.Contains("Too many") == true)
            {
                return ResendVerificationEmailResult.Failed(sendResult.ErrorMessage);
            }

            // For other errors, return generic message
            _logger.LogError("Failed to resend verification email for user {UserId}: {Error}",
                user.Id, sendResult.ErrorMessage);
            return ResendVerificationEmailResult.Failed("Failed to send verification email. Please try again later.");
        }

        return ResendVerificationEmailResult.Succeeded(GenericMessage);
    }
}
