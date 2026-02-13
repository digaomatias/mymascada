using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.AiChat.DTOs;

namespace MyMascada.Application.Features.AiChat.Commands;

public class SendChatMessageCommand : IRequest<SendChatMessageResponse>
{
    public string Content { get; set; } = string.Empty;
}

public class SendChatMessageCommandHandler : IRequestHandler<SendChatMessageCommand, SendChatMessageResponse>
{
    private readonly IAiChatService _aiChatService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IChatMessageRepository _chatMessageRepository;

    public SendChatMessageCommandHandler(
        IAiChatService aiChatService,
        ICurrentUserService currentUserService,
        IChatMessageRepository chatMessageRepository)
    {
        _aiChatService = aiChatService;
        _currentUserService = currentUserService;
        _chatMessageRepository = chatMessageRepository;
    }

    public async Task<SendChatMessageResponse> Handle(SendChatMessageCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();
        var result = await _aiChatService.SendMessageAsync(userId, request.Content);

        if (!result.Success)
        {
            return new SendChatMessageResponse
            {
                Success = false,
                Error = result.Error
            };
        }

        // Retrieve the most recent user and assistant messages for the response
        var recentMessages = await _chatMessageRepository.GetRecentMessagesAsync(userId, 2);
        var messageList = recentMessages.ToList();

        var assistantMsg = messageList.FirstOrDefault(m => m.Role == "assistant");
        var userMsg = messageList.FirstOrDefault(m => m.Role == "user");

        return new SendChatMessageResponse
        {
            Success = true,
            UserMessage = userMsg != null
                ? new ChatMessageDto
                {
                    Id = userMsg.Id,
                    Role = userMsg.Role,
                    Content = userMsg.Content,
                    CreatedAt = userMsg.CreatedAt
                }
                : null,
            AssistantMessage = assistantMsg != null
                ? new ChatMessageDto
                {
                    Id = assistantMsg.Id,
                    Role = assistantMsg.Role,
                    Content = assistantMsg.Content,
                    CreatedAt = assistantMsg.CreatedAt
                }
                : null
        };
    }
}
