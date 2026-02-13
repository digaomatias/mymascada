using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.AiChat.DTOs;

namespace MyMascada.Application.Features.AiChat.Queries;

public class GetChatHistoryQuery : IRequest<ChatHistoryResponse>
{
    public int Limit { get; set; } = 50;
    public int? BeforeId { get; set; }
}

public class GetChatHistoryQueryHandler : IRequestHandler<GetChatHistoryQuery, ChatHistoryResponse>
{
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetChatHistoryQueryHandler(
        IChatMessageRepository chatMessageRepository,
        ICurrentUserService currentUserService)
    {
        _chatMessageRepository = chatMessageRepository;
        _currentUserService = currentUserService;
    }

    public async Task<ChatHistoryResponse> Handle(GetChatHistoryQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();
        var (messages, hasMore) = await _chatMessageRepository.GetMessageHistoryAsync(
            userId, request.Limit, request.BeforeId);

        // Repository returns newest-first for efficient cursor pagination,
        // but the frontend needs chronological order (oldest first) for display
        return new ChatHistoryResponse
        {
            Messages = messages.OrderBy(m => m.CreatedAt).Select(m => new ChatMessageDto
            {
                Id = m.Id,
                Role = m.Role,
                Content = m.Content,
                CreatedAt = m.CreatedAt
            }),
            HasMore = hasMore
        };
    }
}
