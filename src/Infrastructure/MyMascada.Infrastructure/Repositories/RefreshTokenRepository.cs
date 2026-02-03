using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ApplicationDbContext _context;

    public RefreshTokenRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token)
    {
        return await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == token);
    }

    public async Task<IEnumerable<RefreshToken>> GetActiveTokensByUserIdAsync(Guid userId)
    {
        return await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.IsActive)
            .OrderByDescending(rt => rt.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(RefreshToken refreshToken)
    {
        await _context.RefreshTokens.AddAsync(refreshToken);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(RefreshToken refreshToken)
    {
        _context.RefreshTokens.Update(refreshToken);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(RefreshToken refreshToken)
    {
        _context.RefreshTokens.Remove(refreshToken);
        await _context.SaveChangesAsync();
    }

    public async Task RevokeAllUserTokensAsync(Guid userId, string ipAddress)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.IsActive)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.Revoke(ipAddress);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<int> DeleteExpiredAndRevokedTokensAsync(DateTime olderThan)
    {
        var tokensToDelete = await _context.RefreshTokens
            .Where(rt => (rt.ExpiryDate < DateTime.UtcNow || rt.IsRevoked) && rt.CreatedAt < olderThan)
            .ToListAsync();

        if (tokensToDelete.Count == 0)
            return 0;

        _context.RefreshTokens.RemoveRange(tokensToDelete);
        await _context.SaveChangesAsync();
        return tokensToDelete.Count;
    }
}
