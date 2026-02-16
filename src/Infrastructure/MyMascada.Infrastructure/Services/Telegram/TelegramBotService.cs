using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Infrastructure.Services.Telegram.Models;

namespace MyMascada.Infrastructure.Services.Telegram;

public class TelegramBotService : ITelegramBotService
{
    private const string TelegramApiBase = "https://api.telegram.org";
    private const int MaxMessageLength = 4096;

    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramBotService> _logger;

    public TelegramBotService(HttpClient httpClient, ILogger<TelegramBotService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<TelegramBotInfo?> VerifyTokenAsync(string botToken)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{TelegramApiBase}/bot{botToken}/getMe");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Telegram getMe failed with status {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<TelegramResponse<TelegramBotUser>>(content);

            if (result?.Ok != true || result.Result == null)
            {
                return null;
            }

            return new TelegramBotInfo
            {
                BotId = result.Result.Id,
                Username = result.Result.Username ?? string.Empty,
                FirstName = result.Result.FirstName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Telegram bot token");
            return null;
        }
    }

    public async Task<bool> SetWebhookAsync(string botToken, string url, string secret)
    {
        try
        {
            var payload = new
            {
                url,
                secret_token = secret,
                allowed_updates = new[] { "message" }
            };

            var json = JsonSerializer.Serialize(payload);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{TelegramApiBase}/bot{botToken}/setWebhook", httpContent);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Telegram setWebhook failed: {StatusCode} {Body}", response.StatusCode, body);
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<TelegramResponse<bool>>(content);
            return result?.Ok == true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting Telegram webhook");
            return false;
        }
    }

    public async Task<bool> DeleteWebhookAsync(string botToken)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"{TelegramApiBase}/bot{botToken}/deleteWebhook",
                new StringContent("{}", Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Telegram deleteWebhook failed with status {StatusCode}", response.StatusCode);
                return false;
            }

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<TelegramResponse<bool>>(content);
            return result?.Ok == true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Telegram webhook");
            return false;
        }
    }

    public async Task<bool> SendMessageAsync(string botToken, long chatId, string text, string? parseMode = null)
    {
        try
        {
            // Split long messages at paragraph boundaries
            var chunks = SplitMessage(text);

            foreach (var chunk in chunks)
            {
                var success = await SendSingleMessageAsync(botToken, chatId, chunk, parseMode);
                if (!success && parseMode == "MarkdownV2")
                {
                    // Fall back to plain text if MarkdownV2 fails
                    var plainText = TelegramMarkdownConverter.EscapeMarkdownV2(chunk);
                    success = await SendSingleMessageAsync(botToken, chatId, plainText, null);
                }

                if (!success)
                {
                    // Last resort: send without any formatting
                    await SendSingleMessageAsync(botToken, chatId, chunk, null);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Telegram message to chat {ChatId}", chatId);
            return false;
        }
    }

    public async Task SendTypingActionAsync(string botToken, long chatId)
    {
        try
        {
            var payload = new { chat_id = chatId, action = "typing" };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await _httpClient.PostAsync($"{TelegramApiBase}/bot{botToken}/sendChatAction", content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error sending typing action to chat {ChatId}", chatId);
        }
    }

    private async Task<bool> SendSingleMessageAsync(string botToken, long chatId, string text, string? parseMode)
    {
        var payload = new Dictionary<string, object>
        {
            ["chat_id"] = chatId,
            ["text"] = text
        };

        if (!string.IsNullOrEmpty(parseMode))
        {
            payload["parse_mode"] = parseMode;
        }

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{TelegramApiBase}/bot{botToken}/sendMessage", content);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Telegram sendMessage failed: {StatusCode} {Body}", response.StatusCode, body);
            return false;
        }

        return true;
    }

    private static List<string> SplitMessage(string text)
    {
        if (text.Length <= MaxMessageLength)
        {
            return [text];
        }

        var chunks = new List<string>();
        var remaining = text;

        while (remaining.Length > MaxMessageLength)
        {
            // Try to split at paragraph boundary
            var splitIndex = remaining.LastIndexOf("\n\n", MaxMessageLength, StringComparison.Ordinal);
            if (splitIndex <= 0)
            {
                // Try single newline
                splitIndex = remaining.LastIndexOf('\n', MaxMessageLength);
            }
            if (splitIndex <= 0)
            {
                // Force split at max length
                splitIndex = MaxMessageLength;
            }

            chunks.Add(remaining[..splitIndex].TrimEnd());
            remaining = remaining[splitIndex..].TrimStart();
        }

        if (remaining.Length > 0)
        {
            chunks.Add(remaining);
        }

        return chunks;
    }
}
