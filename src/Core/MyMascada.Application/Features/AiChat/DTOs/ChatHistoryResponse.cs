namespace MyMascada.Application.Features.AiChat.DTOs;

public class ChatHistoryResponse
{
    public IEnumerable<ChatMessageDto> Messages { get; set; } = [];
    public bool HasMore { get; set; }
}
