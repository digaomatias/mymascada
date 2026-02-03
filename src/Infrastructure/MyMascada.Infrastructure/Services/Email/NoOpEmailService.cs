using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Email.DTOs;

namespace MyMascada.Infrastructure.Services.Email;

/// <summary>
/// Null Object implementation of IEmailService used when email is not configured.
/// Registered at DI startup instead of real providers so consumers never need
/// to check whether email is available.
/// </summary>
public class NoOpEmailService : IEmailService
{
    private readonly ILogger<NoOpEmailService> _logger;

    public string ProviderId => "noop";
    public string DisplayName => "No-Op (Email Disabled)";
    public bool SupportsAttachments => false;

    public NoOpEmailService(ILogger<NoOpEmailService> logger)
    {
        _logger = logger;
        _logger.LogWarning("Email sending is disabled. Configure Email settings to enable.");
    }

    public Task<EmailResult> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation("Email send suppressed (not configured). Recipient count: {Count}",
            message.To?.Count() ?? 0);
        return Task.FromResult(EmailResult.Failed("Email is not configured for this instance."));
    }

    public Task<EmailResult> SendTemplateAsync(TemplatedEmailMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation("Templated email send suppressed (not configured). Template: {Template}",
            message.TemplateName);
        return Task.FromResult(EmailResult.Failed("Email is not configured for this instance."));
    }

    public Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        return Task.FromResult(false);
    }
}
