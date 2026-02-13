namespace MyMascada.Application.Common.Interfaces;

public interface IAiChatService
{
    Task<AiChatResponse> SendMessageAsync(Guid userId, string message);
}

public class AiChatResponse
{
    public bool Success { get; set; }
    public int? MessageId { get; set; }
    public string? Content { get; set; }
    public string? Error { get; set; }
}
