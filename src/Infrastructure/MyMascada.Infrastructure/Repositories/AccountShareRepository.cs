using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class AccountShareRepository : IAccountShareRepository
{
    private readonly ApplicationDbContext _context;

    public AccountShareRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AccountShare?> GetByIdAsync(int id)
    {
        return await _context.AccountShares
            .Include(s => s.Account)
            .Include(s => s.SharedWithUser)
            .Include(s => s.SharedByUser)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<AccountShare?> GetByIdAsync(int id, int accountId)
    {
        return await _context.AccountShares
            .Include(s => s.Account)
            .Include(s => s.SharedWithUser)
            .Include(s => s.SharedByUser)
            .FirstOrDefaultAsync(s => s.Id == id && s.AccountId == accountId);
    }

    public async Task<IEnumerable<AccountShare>> GetByAccountIdAsync(int accountId)
    {
        return await _context.AccountShares
            .Include(s => s.SharedWithUser)
            .Include(s => s.SharedByUser)
            .Where(s => s.AccountId == accountId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<AccountShare>> GetBySharedWithUserIdAsync(Guid userId)
    {
        return await _context.AccountShares
            .Include(s => s.Account)
            .Include(s => s.SharedByUser)
            .Where(s => s.SharedWithUserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<AccountShare>> GetAcceptedSharesForUserAsync(Guid userId)
    {
        return await _context.AccountShares
            .Include(s => s.Account)
            .Include(s => s.SharedByUser)
            .Where(s => s.SharedWithUserId == userId && s.Status == AccountShareStatus.Accepted)
            .ToListAsync();
    }

    public async Task<AccountShare?> GetByInvitationTokenAsync(string tokenHash)
    {
        return await _context.AccountShares
            .Include(s => s.Account)
            .Include(s => s.SharedByUser)
            .Include(s => s.SharedWithUser)
            .FirstOrDefaultAsync(s => s.InvitationToken == tokenHash && s.Status == AccountShareStatus.Pending);
    }

    public async Task<AccountShare?> GetActiveShareAsync(int accountId, Guid sharedWithUserId)
    {
        return await _context.AccountShares
            .FirstOrDefaultAsync(s =>
                s.AccountId == accountId &&
                s.SharedWithUserId == sharedWithUserId &&
                (s.Status == AccountShareStatus.Pending || s.Status == AccountShareStatus.Accepted));
    }

    public async Task<int> GetPendingCountForAccountAsync(int accountId)
    {
        return await _context.AccountShares
            .CountAsync(s => s.AccountId == accountId && s.Status == AccountShareStatus.Pending);
    }

    public async Task<AccountShare> AddAsync(AccountShare share)
    {
        await _context.AccountShares.AddAsync(share);
        await _context.SaveChangesAsync();
        return share;
    }

    public async Task UpdateAsync(AccountShare share)
    {
        _context.AccountShares.Update(share);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(AccountShare share)
    {
        share.IsDeleted = true;
        share.DeletedAt = DateTimeProvider.UtcNow;
        _context.AccountShares.Update(share);
        await _context.SaveChangesAsync();
    }

    public async Task RevokeSharesByAccountIdAsync(int accountId)
    {
        var activeShares = await _context.AccountShares
            .Where(s => s.AccountId == accountId &&
                       (s.Status == AccountShareStatus.Pending || s.Status == AccountShareStatus.Accepted))
            .ToListAsync();

        if (activeShares.Count == 0)
            return;

        var now = DateTimeProvider.UtcNow;
        foreach (var share in activeShares)
        {
            share.Status = AccountShareStatus.Revoked;
            share.InvitationToken = null;
            share.UpdatedAt = now;
            share.IsDeleted = true;
            share.DeletedAt = now;
        }

        _context.AccountShares.UpdateRange(activeShares);
        await _context.SaveChangesAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
