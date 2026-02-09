using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Email.DTOs;

namespace MyMascada.Infrastructure.Services.Email.Providers;

/// <summary>
/// SMTP email provider using MailKit. Works with Postal, Brevo, Mailtrap, and standard SMTP servers.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly IEmailTemplateService _templateService;
    private readonly IApplicationLogger<SmtpEmailService> _logger;

    public string ProviderId => "smtp";
    public string DisplayName => "SMTP (MailKit)";
    public bool SupportsAttachments => true;

    public SmtpEmailService(
        IOptions<EmailOptions> options,
        IEmailTemplateService templateService,
        IApplicationLogger<SmtpEmailService> logger)
    {
        _options = options.Value;
        _templateService = templateService;
        _logger = logger;
    }

    public async Task<EmailResult> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Email sending disabled. Would send to {To}: {Subject}",
                message.To, message.Subject);
            return EmailResult.Succeeded("disabled-mode");
        }

        try
        {
            var mimeMessage = BuildMimeMessage(message);
            var smtp = _options.Smtp;

            using var client = new SmtpClient();

            // Determine socket options based on SSL/TLS settings
            SecureSocketOptions socketOptions;
            if (!smtp.UseSsl)
            {
                socketOptions = SecureSocketOptions.None;
            }
            else if (smtp.UseStartTls)
            {
                socketOptions = SecureSocketOptions.StartTls;
            }
            else
            {
                socketOptions = SecureSocketOptions.SslOnConnect;
            }

            client.Timeout = smtp.TimeoutSeconds * 1000;

            await client.ConnectAsync(smtp.Host, smtp.Port, socketOptions, ct);

            if (!string.IsNullOrEmpty(smtp.Username) && client.Capabilities.HasFlag(SmtpCapabilities.Authentication))
            {
                await client.AuthenticateAsync(smtp.Username, smtp.Password, ct);
            }

            var response = await client.SendAsync(mimeMessage, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation(
                "Email sent via SMTP to {To}: {Subject} (MessageId: {MessageId}, CorrelationId: {CorrelationId})",
                message.To, message.Subject, mimeMessage.MessageId, message.CorrelationId);

            return EmailResult.Succeeded(mimeMessage.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send failed to {To}: {Subject} (CorrelationId: {CorrelationId})",
                message.To, message.Subject, message.CorrelationId);
            return EmailResult.Failed(ex.Message, "SMTP_ERROR");
        }
    }

    public async Task<EmailResult> SendTemplateAsync(TemplatedEmailMessage message, CancellationToken ct = default)
    {
        try
        {
            var (subject, body) = await _templateService.RenderAsync(
                message.TemplateName,
                message.TemplateData,
                message.Locale,
                ct);

            return await SendAsync(new EmailMessage
            {
                To = message.To,
                ToName = message.ToName,
                Subject = subject,
                Body = body,
                IsHtml = true,
                From = message.From,
                ReplyTo = message.ReplyTo,
                CorrelationId = message.CorrelationId
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render template '{Template}' for email to {To}",
                message.TemplateName, message.To);
            return EmailResult.Failed($"Template error: {ex.Message}", "TEMPLATE_ERROR");
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var smtp = _options.Smtp;

            if (string.IsNullOrEmpty(smtp.Host))
            {
                _logger.LogWarning("SMTP health check failed: Host not configured");
                return false;
            }

            using var client = new SmtpClient();
            client.Timeout = 5000; // 5 second timeout for health check

            SecureSocketOptions socketOptions;
            if (!smtp.UseSsl)
            {
                socketOptions = SecureSocketOptions.None;
            }
            else if (smtp.UseStartTls)
            {
                socketOptions = SecureSocketOptions.StartTls;
            }
            else
            {
                socketOptions = SecureSocketOptions.SslOnConnect;
            }

            await client.ConnectAsync(smtp.Host, smtp.Port, socketOptions, ct);
            await client.DisconnectAsync(true, ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMTP health check failed");
            return false;
        }
    }

    private MimeMessage BuildMimeMessage(EmailMessage message)
    {
        var mimeMessage = new MimeMessage();

        mimeMessage.From.Add(new MailboxAddress(
            message.FromName ?? _options.DefaultFromName,
            message.From ?? _options.DefaultFromEmail));

        mimeMessage.To.Add(new MailboxAddress(message.ToName ?? string.Empty, message.To));
        mimeMessage.Subject = message.Subject;

        if (!string.IsNullOrEmpty(message.ReplyTo))
        {
            mimeMessage.ReplyTo.Add(MailboxAddress.Parse(message.ReplyTo));
        }

        // Add custom headers
        if (message.Headers != null)
        {
            foreach (var header in message.Headers)
            {
                mimeMessage.Headers.Add(header.Key, header.Value);
            }
        }

        // Add correlation ID as header if provided
        if (!string.IsNullOrEmpty(message.CorrelationId))
        {
            mimeMessage.Headers.Add("X-Correlation-ID", message.CorrelationId);
        }

        var builder = new BodyBuilder();

        if (message.IsHtml)
        {
            builder.HtmlBody = message.Body;
        }
        else
        {
            builder.TextBody = message.Body;
        }

        // Add attachments
        if (message.Attachments != null)
        {
            foreach (var att in message.Attachments)
            {
                builder.Attachments.Add(att.FileName, att.Content, ContentType.Parse(att.ContentType));
            }
        }

        mimeMessage.Body = builder.ToMessageBody();
        return mimeMessage;
    }
}
