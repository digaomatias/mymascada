namespace MyMascada.Application.Features.AiChat.DTOs;

public class SendChatMessageResponse
{
    public bool Success { get; set; }
    public ChatMessageDto? UserMessage { get; set; }
    public ChatMessageDto? AssistantMessage { get; set; }
    public string? Error { get; set; }
}
