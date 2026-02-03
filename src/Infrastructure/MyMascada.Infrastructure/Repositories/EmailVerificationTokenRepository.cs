using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class EmailVerificationTokenRepository : IEmailVerificationTokenRepository
{
    private readonly ApplicationDbContext _context;

    public EmailVerificationTokenRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EmailVerificationToken?> GetByTokenHashAsync(string tokenHash)
    {
        return await _context.EmailVerificationTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
    }

    public async Task<int> GetRecentRequestCountAsync(Guid userId, TimeSpan window)
    {
        var cutoffTime = DateTime.UtcNow - window;

        return await _context.EmailVerificationTokens
            .Where(t => t.UserId == userId && t.CreatedAt >= cutoffTime)
            .CountAsync();
    }

    public async Task AddAsync(EmailVerificationToken token)
    {
        await _context.EmailVerificationTokens.AddAsync(token);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(EmailVerificationToken token)
    {
        _context.EmailVerificationTokens.Update(token);
        await _context.SaveChangesAsync();
    }

    public async Task InvalidateAllForUserAsync(Guid userId)
    {
        var activeTokens = await _context.EmailVerificationTokens
            .Where(t => t.UserId == userId && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.MarkAsUsed();
        }

        await _context.SaveChangesAsync();
    }
}
