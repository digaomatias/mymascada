using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.Services.Telegram;
using MyMascada.Infrastructure.Services.Telegram.Models;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/telegram/webhook")]
[AllowAnonymous]
public class TelegramWebhookController : ControllerBase
{
    private readonly IUserTelegramSettingsRepository _telegramSettingsRepository;
    private readonly ISettingsEncryptionService _encryptionService;
    private readonly IAiChatService _aiChatService;
    private readonly ITelegramBotService _telegramBotService;
    private readonly IUserAiSettingsRepository _aiSettingsRepository;
    private readonly ILogger<TelegramWebhookController> _logger;

    public TelegramWebhookController(
        IUserTelegramSettingsRepository telegramSettingsRepository,
        ISettingsEncryptionService encryptionService,
        IAiChatService aiChatService,
        ITelegramBotService telegramBotService,
        IUserAiSettingsRepository aiSettingsRepository,
        ILogger<TelegramWebhookController> logger)
    {
        _telegramSettingsRepository = telegramSettingsRepository;
        _encryptionService = encryptionService;
        _aiChatService = aiChatService;
        _telegramBotService = telegramBotService;
        _aiSettingsRepository = aiSettingsRepository;
        _logger = logger;
    }

    [HttpPost("{secret}")]
    public async Task<IActionResult> HandleWebhook(string secret)
    {
        // Always return 200 to prevent Telegram retries
        try
        {
            // Read the raw body
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            var update = JsonSerializer.Deserialize<TelegramUpdate>(body);
            if (update?.Message == null)
            {
                return Ok();
            }

            var message = update.Message;

            // Ignore non-text messages (photos, stickers, etc.)
            if (string.IsNullOrEmpty(message.Text))
            {
                return Ok();
            }

            // Look up user by webhook secret
            var settings = await _telegramSettingsRepository.GetByWebhookSecretAsync(secret);
            if (settings == null)
            {
                _logger.LogWarning("Webhook called with unknown secret");
                return Ok();
            }

            var chatId = message.Chat.Id;
            string botToken;

            try
            {
                botToken = _encryptionService.Decrypt(settings.EncryptedBotToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt bot token for user {UserId}", settings.UserId);
                return Ok();
            }

            var text = message.Text.Trim();

            // Handle bot commands
            if (text.StartsWith('/'))
            {
                await HandleCommand(botToken, chatId, text, settings.UserId);
                return Ok();
            }

            // Check if user has AI chat configured
            var aiSettings = await _aiSettingsRepository.GetByUserIdAsync(settings.UserId, "chat");
            if (aiSettings == null)
            {
                await _telegramBotService.SendMessageAsync(
                    botToken, chatId,
                    "Please configure your AI Chat settings first at your MyMascada web app (Settings > AI Configuration > Alce tab).");
                return Ok();
            }

            // Send typing indicator
            await _telegramBotService.SendTypingActionAsync(botToken, chatId);

            // Process through the AI chat service
            var response = await _aiChatService.SendMessageAsync(settings.UserId, text);

            if (response.Success && !string.IsNullOrEmpty(response.Content))
            {
                // Convert markdown to Telegram MarkdownV2
                var telegramText = TelegramMarkdownConverter.ConvertToTelegramMarkdown(response.Content);
                await _telegramBotService.SendMessageAsync(botToken, chatId, telegramText, "MarkdownV2");
            }
            else
            {
                var errorMsg = response.Error ?? "Sorry, I couldn't process your message. Please try again.";
                await _telegramBotService.SendMessageAsync(botToken, chatId, errorMsg);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Telegram webhook");
        }

        return Ok();
    }

    private async Task HandleCommand(string botToken, long chatId, string command, Guid userId)
    {
        // Strip bot username from command (e.g., /start@MyBotName → /start)
        var cmd = command.Split('@')[0].ToLowerInvariant();

        switch (cmd)
        {
            case "/start":
                await _telegramBotService.SendMessageAsync(
                    botToken, chatId,
                    "Welcome to MyMascada! I'm Alce, your AI financial assistant.\n\n" +
                    "You can ask me about your finances, balances, spending patterns, and more.\n\n" +
                    "Commands:\n" +
                    "/help - Show this help message\n" +
                    "/clear - Clear chat history");
                break;

            case "/help":
                await _telegramBotService.SendMessageAsync(
                    botToken, chatId,
                    "I can help you with:\n" +
                    "- Account balances and summaries\n" +
                    "- Spending analysis and patterns\n" +
                    "- Transaction search\n" +
                    "- Budget tracking\n" +
                    "- Upcoming bills\n\n" +
                    "Just type your question naturally!");
                break;

            case "/clear":
                // Note: chat history clearing is handled through the web app
                await _telegramBotService.SendMessageAsync(
                    botToken, chatId,
                    "To clear your chat history, please use the MyMascada web app.");
                break;

            default:
                await _telegramBotService.SendMessageAsync(
                    botToken, chatId,
                    "Unknown command. Type /help for available commands.");
                break;
        }
    }
}
