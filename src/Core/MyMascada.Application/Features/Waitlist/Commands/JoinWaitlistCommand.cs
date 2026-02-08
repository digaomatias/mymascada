using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Email.DTOs;
using MyMascada.Application.Features.Waitlist.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Waitlist.Commands;

public class JoinWaitlistCommand : IRequest<JoinWaitlistResponse>
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Locale { get; set; } = "en-US";
    public string? IpAddress { get; set; }
}

public class JoinWaitlistCommandHandler : IRequestHandler<JoinWaitlistCommand, JoinWaitlistResponse>
{
    private readonly IWaitlistRepository _waitlistRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IValidator<JoinWaitlistCommand> _validator;
    private readonly ILogger<JoinWaitlistCommandHandler> _logger;

    public JoinWaitlistCommandHandler(
        IWaitlistRepository waitlistRepository,
        IUserRepository userRepository,
        IEmailService emailService,
        IEmailTemplateService emailTemplateService,
        IValidator<JoinWaitlistCommand> validator,
        ILogger<JoinWaitlistCommandHandler> logger)
    {
        _waitlistRepository = waitlistRepository;
        _userRepository = userRepository;
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _validator = validator;
        _logger = logger;
    }

    public async Task<JoinWaitlistResponse> Handle(JoinWaitlistCommand request, CancellationToken cancellationToken)
    {
        var response = new JoinWaitlistResponse();

        // Validate
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            response.Errors.AddRange(validationResult.Errors.Select(e => e.ErrorMessage));
            return response;
        }

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();

        // Check if already on waitlist (silent success to prevent enumeration)
        var existing = await _waitlistRepository.GetByNormalizedEmailAsync(normalizedEmail);
        if (existing != null)
        {
            _logger.LogInformation("Waitlist signup attempted for existing email");
            response.IsSuccess = true;
            response.Message = "You're on the list! Check your inbox for a confirmation.";
            return response;
        }

        // Check if email already has an account
        if (await _userRepository.ExistsByEmailAsync(request.Email))
        {
            response.IsSuccess = true;
            response.Message = "You already have an account!";
            return response;
        }

        // Create waitlist entry
        var entry = new WaitlistEntry
        {
            Id = Guid.NewGuid(),
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            Name = request.Name.Trim(),
            Locale = request.Locale,
            Source = "landing-page",
            IpAddress = request.IpAddress,
            Status = WaitlistStatus.Pending
        };

        await _waitlistRepository.AddAsync(entry);
        _logger.LogInformation("New waitlist entry created for {Email}", entry.Email);

        // Send confirmation email (best effort - don't fail if email fails)
        try
        {
            var locale = request.Locale == "pt-BR" ? "pt-BR" : "en-US";
            var templateData = new Dictionary<string, object>
            {
                { "Name", entry.Name },
                { "CurrentYear", DateTime.UtcNow.Year }
            };

            var (subject, body) = await _emailTemplateService.RenderAsync(
                "waitlist-confirmation", templateData, locale, cancellationToken);

            await _emailService.SendAsync(new EmailMessage
            {
                To = entry.Email,
                ToName = entry.Name,
                Subject = subject,
                Body = body,
                IsHtml = true
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send waitlist confirmation email to {Email}", entry.Email);
        }

        response.IsSuccess = true;
        response.Message = "You're on the list! Check your inbox for a confirmation.";
        return response;
    }
}
