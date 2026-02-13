using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

public interface IChatMessageRepository
{
    Task<IEnumerable<ChatMessage>> GetRecentMessagesAsync(Guid userId, int limit = 20);
    Task<(IEnumerable<ChatMessage> Messages, bool HasMore)> GetMessageHistoryAsync(Guid userId, int limit = 50, int? beforeId = null);
    Task<ChatMessage> AddAsync(ChatMessage message);
    Task DeleteAllForUserAsync(Guid userId);
    Task<int> GetMessageCountAsync(Guid userId);
}
