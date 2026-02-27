using MyMascada.Domain.Common;

namespace MyMascada.Domain.Entities;

public class UserTelegramSettings : BaseEntity<int>
{
    public Guid UserId { get; set; }
    public string EncryptedBotToken { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string? BotUsername { get; set; }
    public long? ChatId { get; set; }
    public bool IsActive { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? LastVerifiedAt { get; set; }
}
