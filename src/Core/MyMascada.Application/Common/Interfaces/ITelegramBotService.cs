namespace MyMascada.Application.Common.Interfaces;

public interface ITelegramBotService
{
    Task<TelegramBotInfo?> VerifyTokenAsync(string botToken);
    Task<bool> SetWebhookAsync(string botToken, string url, string secret);
    Task<bool> DeleteWebhookAsync(string botToken);
    Task<bool> SendMessageAsync(string botToken, long chatId, string text, string? parseMode = null);
    Task SendTypingActionAsync(string botToken, long chatId);
}

public class TelegramBotInfo
{
    public long BotId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
}
