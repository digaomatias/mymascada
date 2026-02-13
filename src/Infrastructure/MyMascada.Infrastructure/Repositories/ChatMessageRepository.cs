using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class ChatMessageRepository : IChatMessageRepository
{
    private readonly ApplicationDbContext _context;

    public ChatMessageRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ChatMessage>> GetRecentMessagesAsync(Guid userId, int limit = 20)
    {
        return await _context.ChatMessages
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<(IEnumerable<ChatMessage> Messages, bool HasMore)> GetMessageHistoryAsync(Guid userId, int limit = 50, int? beforeId = null)
    {
        var query = _context.ChatMessages
            .Where(m => m.UserId == userId);

        if (beforeId.HasValue)
        {
            query = query.Where(m => m.Id < beforeId.Value);
        }

        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit + 1)
            .ToListAsync();

        var hasMore = messages.Count > limit;

        if (hasMore)
        {
            messages = messages.Take(limit).ToList();
        }

        // Return newest first for UI scrollback
        return (messages, hasMore);
    }

    public async Task<ChatMessage> AddAsync(ChatMessage message)
    {
        await _context.ChatMessages.AddAsync(message);
        await _context.SaveChangesAsync();
        return message;
    }

    public async Task DeleteAllForUserAsync(Guid userId)
    {
        var messages = await _context.ChatMessages
            .Where(m => m.UserId == userId)
            .ToListAsync();

        foreach (var message in messages)
        {
            message.IsDeleted = true;
            message.DeletedAt = DateTimeProvider.UtcNow;
        }

        _context.ChatMessages.UpdateRange(messages);
        await _context.SaveChangesAsync();
    }

    public async Task<int> GetMessageCountAsync(Guid userId)
    {
        return await _context.ChatMessages
            .CountAsync(m => m.UserId == userId);
    }
}
