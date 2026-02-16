namespace MyMascada.Application.Features.Telegram.DTOs;

public class TelegramSettingsDto
{
    public bool HasSettings { get; set; }
    public string? BotUsername { get; set; }
    public bool IsActive { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? LastVerifiedAt { get; set; }
}
