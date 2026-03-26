using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.BankConnections.Commands;
using MyMascada.Application.Features.BankConnections.DTOs;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/webhooks/akahu")]
[AllowAnonymous]
public class AkahuWebhookController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAkahuWebhookSignatureService _signatureService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AkahuWebhookController> _logger;

    private const string ReplayCachePrefix = "akahu_webhook_";
    private static readonly TimeSpan ReplayWindow = TimeSpan.FromHours(24);
    private static readonly SemaphoreSlim ReplayLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public AkahuWebhookController(
        IMediator mediator,
        IAkahuWebhookSignatureService signatureService,
        IMemoryCache cache,
        ILogger<AkahuWebhookController> logger)
    {
        _mediator = mediator;
        _signatureService = signatureService;
        _cache = cache;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> HandleWebhook()
    {
        // Read raw body first — needed for signature verification
        string body;
        using (var reader = new StreamReader(HttpContext.Request.Body))
        {
            body = await reader.ReadToEndAsync();
        }

        // Extract signature headers
        var signature = Request.Headers["X-Akahu-Signature"].FirstOrDefault();
        var signingKeyId = Request.Headers["X-Akahu-Signing-Key"].FirstOrDefault();

        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(signingKeyId))
        {
            _logger.LogWarning("Akahu webhook received without signature headers");
            return BadRequest();
        }

        // Verify signature
        var isValid = await _signatureService.VerifySignatureAsync(body, signature, signingKeyId);
        if (!isValid)
        {
            _logger.LogWarning("Akahu webhook signature verification failed for key {KeyId}", signingKeyId);
            return BadRequest();
        }

        // Replay protection — hash the body to detect duplicate deliveries
        var bodyHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        var cacheKey = ReplayCachePrefix + bodyHash;

        await ReplayLock.WaitAsync();
        try
        {
            if (_cache.TryGetValue(cacheKey, out _))
            {
                _logger.LogWarning("Akahu webhook replay detected, ignoring duplicate payload");
                return Ok();
            }

            _cache.Set(cacheKey, true, ReplayWindow);
        }
        finally
        {
            ReplayLock.Release();
        }

        // Parse and process — always return 200 to prevent Akahu retries
        try
        {
            var payload = JsonSerializer.Deserialize<AkahuWebhookPayload>(body, JsonOptions);
            if (payload == null)
            {
                _logger.LogWarning("Failed to deserialize Akahu webhook payload");
                return Ok();
            }

            _logger.LogInformation("Akahu webhook received: {Type}/{Code}", payload.WebhookType, payload.WebhookCode);

            await _mediator.Send(new ProcessAkahuWebhookCommand(payload));
        }
        catch (Exception ex)
        {
            // Log but don't throw — return 200 to prevent Akahu from retrying
            _logger.LogError(ex, "Error processing Akahu webhook");
        }

        return Ok();
    }
}
