using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Telegram.DTOs;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using System.Security.Cryptography;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/telegram/settings")]
[Authorize]
public class TelegramSettingsController : ControllerBase
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserTelegramSettingsRepository _repository;
    private readonly ISettingsEncryptionService _encryptionService;
    private readonly ITelegramBotService _telegramBotService;
    private readonly IConfiguration _configuration;
    private readonly AppOptions _appOptions;

    public TelegramSettingsController(
        ICurrentUserService currentUserService,
        IUserTelegramSettingsRepository repository,
        ISettingsEncryptionService encryptionService,
        ITelegramBotService telegramBotService,
        IConfiguration configuration,
        IOptions<AppOptions> appOptions)
    {
        _currentUserService = currentUserService;
        _repository = repository;
        _encryptionService = encryptionService;
        _telegramBotService = telegramBotService;
        _configuration = configuration;
        _appOptions = appOptions.Value;
    }

    [HttpGet]
    public async Task<ActionResult<TelegramSettingsDto>> GetSettings()
    {
        var userId = _currentUserService.GetUserId();
        var settings = await _repository.GetByUserIdAsync(userId);

        if (settings == null)
        {
            return Ok(new TelegramSettingsDto { HasSettings = false });
        }

        return Ok(new TelegramSettingsDto
        {
            HasSettings = true,
            BotUsername = settings.BotUsername,
            IsActive = settings.IsActive,
            IsVerified = settings.IsVerified,
            LastVerifiedAt = settings.LastVerifiedAt
        });
    }

    [HttpPut]
    public async Task<ActionResult<TelegramSettingsDto>> SaveSettings([FromBody] SaveTelegramSettingsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BotToken))
        {
            return BadRequest(new { Error = "Bot token is required." });
        }

        // Verify the token with Telegram
        var botInfo = await _telegramBotService.VerifyTokenAsync(request.BotToken);
        if (botInfo == null)
        {
            return BadRequest(new { Error = "Invalid bot token. Please check the token and try again." });
        }

        var userId = _currentUserService.GetUserId();
        var existing = await _repository.GetByUserIdAsync(userId);

        // If updating, clean up old webhook first
        if (existing != null)
        {
            try
            {
                var oldToken = _encryptionService.Decrypt(existing.EncryptedBotToken);
                await _telegramBotService.DeleteWebhookAsync(oldToken);
            }
            catch
            {
                // Best effort — old token might be invalid
            }
        }

        var encryptedToken = _encryptionService.Encrypt(request.BotToken);
        var webhookSecret = GenerateWebhookSecret();
        var webhookUrl = BuildWebhookUrl(webhookSecret);

        // Set webhook with Telegram
        var webhookSet = await _telegramBotService.SetWebhookAsync(request.BotToken, webhookUrl, webhookSecret);
        if (!webhookSet)
        {
            return StatusCode(502, new { Error = "Failed to register webhook with Telegram. Please try again." });
        }

        if (existing == null)
        {
            var settings = new UserTelegramSettings
            {
                UserId = userId,
                EncryptedBotToken = encryptedToken,
                WebhookSecret = webhookSecret,
                BotUsername = botInfo.Username,
                IsActive = true,
                IsVerified = true,
                LastVerifiedAt = DateTimeProvider.UtcNow
            };

            await _repository.AddAsync(settings);
        }
        else
        {
            existing.EncryptedBotToken = encryptedToken;
            existing.WebhookSecret = webhookSecret;
            existing.BotUsername = botInfo.Username;
            existing.IsActive = true;
            existing.IsVerified = true;
            existing.LastVerifiedAt = DateTimeProvider.UtcNow;
            existing.IsDeleted = false;
            existing.DeletedAt = null;
            existing.UpdatedAt = DateTimeProvider.UtcNow;

            await _repository.UpdateAsync(existing);
        }

        return Ok(new TelegramSettingsDto
        {
            HasSettings = true,
            BotUsername = botInfo.Username,
            IsActive = true,
            IsVerified = true,
            LastVerifiedAt = DateTimeProvider.UtcNow
        });
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteSettings()
    {
        var userId = _currentUserService.GetUserId();
        var settings = await _repository.GetByUserIdAsync(userId);

        if (settings == null)
        {
            return NotFound(new { Error = "No Telegram settings found." });
        }

        // Delete webhook from Telegram
        try
        {
            var botToken = _encryptionService.Decrypt(settings.EncryptedBotToken);
            await _telegramBotService.DeleteWebhookAsync(botToken);
        }
        catch
        {
            // Best effort cleanup
        }

        await _repository.DeleteAsync(settings);

        return Ok(new { Message = "Telegram bot disconnected." });
    }

    [HttpPost("test")]
    public async Task<ActionResult<TelegramTestResult>> TestToken([FromBody] SaveTelegramSettingsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BotToken))
        {
            return BadRequest(new { Error = "Bot token is required." });
        }

        var botInfo = await _telegramBotService.VerifyTokenAsync(request.BotToken);

        if (botInfo == null)
        {
            return Ok(new TelegramTestResult
            {
                Success = false,
                Error = "Invalid bot token. Please check the token and try again."
            });
        }

        return Ok(new TelegramTestResult
        {
            Success = true,
            BotUsername = botInfo.Username,
            BotName = botInfo.FirstName
        });
    }

    private string BuildWebhookUrl(string webhookSecret)
    {
        var baseUrl = _configuration["Telegram:WebhookBaseUrl"];
        if (string.IsNullOrEmpty(baseUrl))
        {
            baseUrl = _appOptions.FrontendUrl;
        }

        // Remove trailing slash
        baseUrl = baseUrl.TrimEnd('/');
        return $"{baseUrl}/api/telegram/webhook/{webhookSecret}";
    }

    private static string GenerateWebhookSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}

public class TelegramTestResult
{
    public bool Success { get; set; }
    public string? BotUsername { get; set; }
    public string? BotName { get; set; }
    public string? Error { get; set; }
}
