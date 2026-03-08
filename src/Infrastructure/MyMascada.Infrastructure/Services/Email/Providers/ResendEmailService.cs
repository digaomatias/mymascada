using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Email.DTOs;

namespace MyMascada.Infrastructure.Services.Email.Providers;

/// <summary>
/// Resend API email provider for transactional email delivery.
/// Uses direct HTTP calls to Resend REST API (https://resend.com/docs/api-reference).
/// </summary>
public class ResendEmailService : IEmailService
{
    private const string ResendApiUrl = "https://api.resend.com/emails";

    private readonly EmailOptions _options;
    private readonly IEmailTemplateService _templateService;
    private readonly IApplicationLogger<ResendEmailService> _logger;
    private readonly HttpClient _httpClient;
    private readonly bool _isConfigured;

    public string ProviderId => "resend";
    public string DisplayName => "Resend API";
    public bool SupportsAttachments => true;

    public ResendEmailService(
        IOptions<EmailOptions> options,
        IEmailTemplateService templateService,
        IApplicationLogger<ResendEmailService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _templateService = templateService;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("Resend");

        _isConfigured = !string.IsNullOrEmpty(_options.Resend.ApiKey);
        if (_isConfigured)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.Resend.ApiKey);
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

        if (!_isConfigured)
        {
            _logger.LogError(null, "Resend not configured - ApiKey missing");
            return EmailResult.Failed("Resend not configured", "NOT_CONFIGURED");
        }

        try
        {
            var payload = new ResendEmailPayload
            {
                From = BuildFromAddress(message),
                To = [message.ToName != null ? $"{message.ToName} <{message.To}>" : message.To],
                Subject = message.Subject,
                Html = message.IsHtml ? message.Body : null,
                Text = message.IsHtml ? null : message.Body,
            };

            if (!string.IsNullOrEmpty(message.ReplyTo))
            {
                payload.ReplyTo = [message.ReplyTo];
            }

            if (message.Headers != null)
            {
                payload.Headers = message.Headers.ToDictionary(kv => kv.Key, kv => kv.Value);
            }

            if (!string.IsNullOrEmpty(message.CorrelationId))
            {
                payload.Headers ??= new Dictionary<string, string>();
                payload.Headers["X-Correlation-ID"] = message.CorrelationId;
            }

            if (message.Attachments?.Any() == true)
            {
                payload.Attachments = message.Attachments.Select(att => new ResendAttachment
                {
                    Content = Convert.ToBase64String(att.Content),
                    Filename = att.FileName,
                    ContentType = att.ContentType
                }).ToList();
            }

            var response = await _httpClient.PostAsJsonAsync(ResendApiUrl, payload, JsonOptions, ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ResendSendResponse>(JsonOptions, ct);
                var messageId = result?.Id ?? "unknown";

                _logger.LogInformation(
                    "Email sent via Resend to {To}: {Subject} (MessageId: {MessageId}, CorrelationId: {CorrelationId})",
                    message.To, message.Subject, messageId, message.CorrelationId);

                return EmailResult.Succeeded(messageId);
            }

            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(null,
                "Resend send failed ({StatusCode}): {Error}",
                (int)response.StatusCode, errorBody);

            return EmailResult.Failed($"Resend API error: {response.StatusCode}", "RESEND_ERROR");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resend send failed to {To}: {Subject} (CorrelationId: {CorrelationId})",
                message.To, message.Subject, message.CorrelationId);
            return EmailResult.Failed(ex.Message, "RESEND_ERROR");
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
        if (!_isConfigured)
        {
            _logger.LogWarning("Resend health check failed: ApiKey not configured");
        }

        return Task.FromResult(_isConfigured);
    }

    private string BuildFromAddress(EmailMessage message)
    {
        var fromEmail = message.From ?? _options.DefaultFromEmail;
        var fromName = message.FromName ?? _options.DefaultFromName;

        return !string.IsNullOrEmpty(fromName)
            ? $"{fromName} <{fromEmail}>"
            : fromEmail;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    #region Resend API DTOs

    private class ResendEmailPayload
    {
        public string From { get; set; } = default!;
        public List<string> To { get; set; } = [];
        public string Subject { get; set; } = default!;
        public string? Html { get; set; }
        public string? Text { get; set; }
        public List<string>? ReplyTo { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
        public List<ResendAttachment>? Attachments { get; set; }
    }

    private class ResendAttachment
    {
        public string Content { get; set; } = default!;
        public string Filename { get; set; } = default!;
        [JsonPropertyName("type")]
        public string? ContentType { get; set; }
    }

    private class ResendSendResponse
    {
        public string? Id { get; set; }
    }

    #endregion
}
