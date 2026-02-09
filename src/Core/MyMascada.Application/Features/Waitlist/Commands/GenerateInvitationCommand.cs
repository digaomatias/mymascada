using System.Security.Cryptography;
using MediatR;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Email.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Waitlist.Commands;

public class GenerateInvitationCommand : IRequest<GenerateInvitationResponse>
{
    public Guid? WaitlistEntryId { get; set; }
    public string? Email { get; set; }
    public int ExpiresInDays { get; set; } = 7;
    public int MaxUses { get; set; } = 1;
    public bool SendEmail { get; set; } = true;
}

public class GenerateInvitationResponse
{
    public bool IsSuccess { get; set; }
    public string? Code { get; set; }
    public Guid? InvitationCodeId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public class GenerateInvitationCommandHandler : IRequestHandler<GenerateInvitationCommand, GenerateInvitationResponse>
{
    private const string UnambiguousChars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 6;
    private const string CodePrefix = "MYMASC-";

    private readonly IInvitationCodeRepository _invitationCodeRepository;
    private readonly IWaitlistRepository _waitlistRepository;
    private readonly IEmailServiceFactory _emailServiceFactory;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly ILogger<GenerateInvitationCommandHandler> _logger;

    public GenerateInvitationCommandHandler(
        IInvitationCodeRepository invitationCodeRepository,
        IWaitlistRepository waitlistRepository,
        IEmailServiceFactory emailServiceFactory,
        IEmailTemplateService emailTemplateService,
        ILogger<GenerateInvitationCommandHandler> logger)
    {
        _invitationCodeRepository = invitationCodeRepository;
        _waitlistRepository = waitlistRepository;
        _emailServiceFactory = emailServiceFactory;
        _emailTemplateService = emailTemplateService;
        _logger = logger;
    }

    public async Task<GenerateInvitationResponse> Handle(GenerateInvitationCommand request, CancellationToken cancellationToken)
    {
        WaitlistEntry? waitlistEntry = null;
        var email = request.Email;
        var name = "there";

        // Load waitlist entry if provided
        if (request.WaitlistEntryId.HasValue)
        {
            waitlistEntry = await _waitlistRepository.GetByIdAsync(request.WaitlistEntryId.Value);
            if (waitlistEntry == null)
            {
                return new GenerateInvitationResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Waitlist entry not found."
                };
            }

            email ??= waitlistEntry.Email;
            name = waitlistEntry.Name;

            // Revoke previous active codes for this entry
            await _invitationCodeRepository.RevokeActiveCodesForEntryAsync(waitlistEntry.Id);
        }

        // Generate unique code
        var codeChars = new char[CodeLength];
        var randomBytes = new byte[CodeLength];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        for (var i = 0; i < CodeLength; i++)
        {
            codeChars[i] = UnambiguousChars[randomBytes[i] % UnambiguousChars.Length];
        }

        var generatedCode = CodePrefix + new string(codeChars);
        var expiresAt = DateTime.UtcNow.AddDays(request.ExpiresInDays);

        // Create invitation code entity
        var invitationCode = new InvitationCode
        {
            Id = Guid.NewGuid(),
            Code = generatedCode,
            NormalizedCode = generatedCode.ToUpperInvariant(),
            WaitlistEntryId = request.WaitlistEntryId,
            ExpiresAt = expiresAt,
            Status = InvitationCodeStatus.Active,
            MaxUses = request.MaxUses,
            UseCount = 0
        };

        await _invitationCodeRepository.AddAsync(invitationCode);

        // Update waitlist entry status if linked
        if (waitlistEntry != null)
        {
            waitlistEntry.Status = WaitlistStatus.Invited;
            waitlistEntry.InvitedAt = DateTime.UtcNow;
            await _waitlistRepository.UpdateAsync(waitlistEntry);
        }

        _logger.LogInformation("Generated invitation code {Code} (expires {ExpiresAt})", generatedCode, expiresAt);

        // Send invitation email if requested
        if (request.SendEmail && !string.IsNullOrWhiteSpace(email))
        {
            try
            {
                var locale = waitlistEntry?.Locale == "pt-BR" ? "pt-BR" : "en-US";
                var templateData = new Dictionary<string, object>
                {
                    { "Name", name },
                    { "InviteCode", generatedCode },
                    { "RegisterUrl", "https://app.mymascada.com/auth/register" },
                    { "ExpirationDays", request.ExpiresInDays },
                    { "CurrentYear", DateTime.UtcNow.Year }
                };

                var (subject, body) = await _emailTemplateService.RenderAsync(
                    "invitation", templateData, locale, cancellationToken);

                var emailService = _emailServiceFactory.GetDefaultProvider();
                await emailService.SendAsync(new EmailMessage
                {
                    To = email,
                    ToName = name,
                    Subject = subject,
                    Body = body,
                    IsHtml = true
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send invitation email to {Email}", email);
            }
        }

        return new GenerateInvitationResponse
        {
            IsSuccess = true,
            Code = generatedCode,
            InvitationCodeId = invitationCode.Id,
            ExpiresAt = expiresAt
        };
    }
}
