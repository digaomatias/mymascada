using MyMascada.Domain.Common;

namespace MyMascada.Domain.Entities;

public class ChatMessage : BaseEntity<int>
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;  // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public int? TokenEstimate { get; set; }
}
