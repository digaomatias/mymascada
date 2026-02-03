using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Email.DTOs;
using PostmarkDotNet;
using PostmarkDotNet.Model;

namespace MyMascada.Infrastructure.Services.Email.Providers;

/// <summary>
/// Postmark API email provider for transactional email delivery.
/// </summary>
public class PostmarkEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly IEmailTemplateService _templateService;
    private readonly IApplicationLogger<PostmarkEmailService> _logger;
    private readonly PostmarkClient? _client;

    public string ProviderId => "postmark";
    public string DisplayName => "Postmark API";
    public bool SupportsAttachments => true;

    public PostmarkEmailService(
        IOptions<EmailOptions> options,
        IEmailTemplateService templateService,
        IApplicationLogger<PostmarkEmailService> logger)
    {
        _options = options.Value;
        _templateService = templateService;
        _logger = logger;

        // Only create client if API token is configured
        if (!string.IsNullOrEmpty(_options.Postmark.ServerToken))
        {
            _client = new PostmarkClient(_options.Postmark.ServerToken);
        }
    }

    public async Task<EmailResult> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Email sending disabled. Would send to {To}: {Subject}",
                message.To, message.Subject);
            return EmailResult.Succeeded("disabled-mode");
        }

        if (_client == null)
        {
            _logger.LogError(null, "Postmark client not initialized - ServerToken not configured");
            return EmailResult.Failed("Postmark not configured", "NOT_CONFIGURED");
        }

        try
        {
            var postmarkMessage = new PostmarkMessage
            {
                To = message.ToName != null ? $"{message.ToName} <{message.To}>" : message.To,
                From = BuildFromAddress(message),
                Subject = message.Subject,
                HtmlBody = message.IsHtml ? message.Body : null,
                TextBody = message.IsHtml ? null : message.Body,
                ReplyTo = message.ReplyTo,
                MessageStream = _options.Postmark.MessageStream
            };

            // Build headers collection
            var headers = new HeaderCollection();

            // Add custom headers
            if (message.Headers != null)
            {
                foreach (var header in message.Headers)
                {
                    headers.Add(new MailHeader(header.Key, header.Value));
                }
            }

            // Add correlation ID as header if provided
            if (!string.IsNullOrEmpty(message.CorrelationId))
            {
                headers.Add(new MailHeader("X-Correlation-ID", message.CorrelationId));
            }

            if (headers.Count > 0)
            {
                postmarkMessage.Headers = headers;
            }

            // Add attachments
            if (message.Attachments?.Any() == true)
            {
                foreach (var att in message.Attachments)
                {
                    postmarkMessage.AddAttachment(
                        att.Content,
                        att.FileName,
                        att.ContentType);
                }
            }

            var response = await _client.SendMessageAsync(postmarkMessage);

            if (response.Status == PostmarkStatus.Success)
            {
                _logger.LogInformation(
                    "Email sent via Postmark to {To}: {Subject} (MessageId: {MessageId}, CorrelationId: {CorrelationId})",
                    message.To, message.Subject, response.MessageID, message.CorrelationId);

                return EmailResult.Succeeded(response.MessageID.ToString());
            }

            _logger.LogError(null,
                "Postmark send failed: {Status} - {Message} (ErrorCode: {ErrorCode})",
                new { Status = response.Status, Message = response.Message, ErrorCode = response.ErrorCode });

            return EmailResult.Failed(response.Message, response.ErrorCode.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Postmark send failed to {To}: {Subject} (CorrelationId: {CorrelationId})",
                message.To, message.Subject, message.CorrelationId);
            return EmailResult.Failed(ex.Message, "POSTMARK_ERROR");
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

    public Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        // Postmark doesn't have a simple ping endpoint
        // Check if the client is configured
        var isConfigured = _client != null && !string.IsNullOrEmpty(_options.Postmark.ServerToken);

        if (!isConfigured)
        {
            _logger.LogWarning("Postmark health check failed: ServerToken not configured");
        }

        return Task.FromResult(isConfigured);
    }

    private string BuildFromAddress(EmailMessage message)
    {
        var fromEmail = message.From ?? _options.DefaultFromEmail;
        var fromName = message.FromName ?? _options.DefaultFromName;

        if (!string.IsNullOrEmpty(fromName))
        {
            return $"{fromName} <{fromEmail}>";
        }

        return fromEmail;
    }
}
