using MyMascada.Application.Features.Email.DTOs;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Core email sending interface. Implementations handle specific providers.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Unique identifier for this email provider (e.g., "smtp", "postmark")
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Human-readable name for logging and diagnostics
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Whether this provider supports file attachments
    /// </summary>
    bool SupportsAttachments { get; }

    /// <summary>
    /// Sends an email message
    /// </summary>
    /// <param name="message">The email message to send</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating success or failure with details</returns>
    Task<EmailResult> SendAsync(EmailMessage message, CancellationToken ct = default);

    /// <summary>
    /// Sends an email using a template
    /// </summary>
    /// <param name="message">The templated email message to send</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating success or failure with details</returns>
    Task<EmailResult> SendTemplateAsync(TemplatedEmailMessage message, CancellationToken ct = default);

    /// <summary>
    /// Checks if the email provider is healthy and reachable
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if the provider is healthy</returns>
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}
