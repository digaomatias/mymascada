using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly ApplicationDbContext _context;

    public PasswordResetTokenRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash)
    {
        return await _context.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
    }

    public async Task<int> GetRecentRequestCountAsync(Guid userId, TimeSpan window)
    {
        var cutoffTime = DateTime.UtcNow - window;

        return await _context.PasswordResetTokens
            .Where(t => t.UserId == userId && t.CreatedAt >= cutoffTime)
            .CountAsync();
    }

    public async Task AddAsync(PasswordResetToken token)
    {
        await _context.PasswordResetTokens.AddAsync(token);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(PasswordResetToken token)
    {
        _context.PasswordResetTokens.Update(token);
        await _context.SaveChangesAsync();
    }

    public async Task InvalidateAllForUserAsync(Guid userId)
    {
        var activeTokens = await _context.PasswordResetTokens
            .Where(t => t.UserId == userId && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.MarkAsUsed();
        }

        await _context.SaveChangesAsync();
    }

    public async Task<int> DeleteExpiredAndUsedTokensAsync(DateTime olderThan)
    {
        var tokensToDelete = await _context.PasswordResetTokens
            .Where(t => (t.IsUsed || t.ExpiresAt < DateTime.UtcNow) && t.CreatedAt < olderThan)
            .ToListAsync();

        if (tokensToDelete.Count == 0)
            return 0;

        _context.PasswordResetTokens.RemoveRange(tokensToDelete);
        await _context.SaveChangesAsync();
        return tokensToDelete.Count;
    }
}
