using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class WalletRepository : IWalletRepository
{
    private readonly ApplicationDbContext _context;

    public WalletRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Wallet>> GetWalletsForUserAsync(Guid userId, bool includeArchived = false, CancellationToken ct = default)
    {
        var query = _context.Wallets
            .Include(w => w.Allocations.Where(a => !a.IsDeleted))
            .Where(w => w.UserId == userId && !w.IsDeleted);

        if (!includeArchived)
        {
            query = query.Where(w => !w.IsArchived);
        }

        return await query
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Wallet?> GetWalletByIdAsync(int walletId, Guid userId, CancellationToken ct = default)
    {
        return await _context.Wallets
            .Include(w => w.Allocations.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.Transaction)
                    .ThenInclude(t => t.Account)
            .FirstOrDefaultAsync(w => w.Id == walletId && w.UserId == userId && !w.IsDeleted, ct);
    }

    public async Task<Wallet> CreateWalletAsync(Wallet wallet, CancellationToken ct = default)
    {
        wallet.CreatedAt = DateTime.UtcNow;
        wallet.UpdatedAt = DateTime.UtcNow;

        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync(ct);

        // Reload with includes
        return await GetWalletByIdAsync(wallet.Id, wallet.UserId, ct)
               ?? throw new InvalidOperationException("Failed to reload created wallet");
    }

    public async Task<Wallet> UpdateWalletAsync(Wallet wallet, CancellationToken ct = default)
    {
        wallet.UpdatedAt = DateTime.UtcNow;

        _context.Wallets.Update(wallet);
        await _context.SaveChangesAsync(ct);

        // Reload with includes
        return await GetWalletByIdAsync(wallet.Id, wallet.UserId, ct)
               ?? throw new InvalidOperationException("Failed to reload updated wallet");
    }

    public async Task DeleteWalletAsync(int walletId, Guid userId, CancellationToken ct = default)
    {
        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.Id == walletId && w.UserId == userId && !w.IsDeleted, ct);

        if (wallet != null)
        {
            wallet.IsDeleted = true;
            wallet.DeletedAt = DateTime.UtcNow;
            wallet.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> WalletNameExistsAsync(Guid userId, string name, int? excludeId = null, CancellationToken ct = default)
    {
        var query = _context.Wallets
            .Where(w => w.UserId == userId && w.Name == name && !w.IsDeleted);

        if (excludeId.HasValue)
        {
            query = query.Where(w => w.Id != excludeId.Value);
        }

        return await query.AnyAsync(ct);
    }

    public async Task<WalletAllocation?> GetAllocationByIdAsync(int allocationId, CancellationToken ct = default)
    {
        return await _context.WalletAllocations
            .Include(a => a.Wallet)
            .Include(a => a.Transaction)
                .ThenInclude(t => t.Account)
            .FirstOrDefaultAsync(a => a.Id == allocationId && !a.IsDeleted, ct);
    }

    public async Task<WalletAllocation> CreateAllocationAsync(WalletAllocation allocation, CancellationToken ct = default)
    {
        allocation.CreatedAt = DateTime.UtcNow;
        allocation.UpdatedAt = DateTime.UtcNow;

        _context.WalletAllocations.Add(allocation);
        await _context.SaveChangesAsync(ct);

        // Reload with includes
        return await GetAllocationByIdAsync(allocation.Id, ct)
               ?? throw new InvalidOperationException("Failed to reload created allocation");
    }

    public async Task DeleteAllocationAsync(int allocationId, CancellationToken ct = default)
    {
        var allocation = await _context.WalletAllocations
            .FirstOrDefaultAsync(a => a.Id == allocationId && !a.IsDeleted, ct);

        if (allocation != null)
        {
            allocation.IsDeleted = true;
            allocation.DeletedAt = DateTime.UtcNow;
            allocation.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<IEnumerable<WalletAllocation>> GetAllocationsForWalletAsync(int walletId, CancellationToken ct = default)
    {
        return await _context.WalletAllocations
            .Include(a => a.Transaction)
                .ThenInclude(t => t.Account)
            .Where(a => a.WalletId == walletId && !a.IsDeleted)
            .OrderByDescending(a => a.Transaction.TransactionDate)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<decimal> GetWalletBalanceAsync(int walletId, CancellationToken ct = default)
    {
        return await _context.WalletAllocations
            .Where(a => a.WalletId == walletId && !a.IsDeleted)
            .SumAsync(a => a.Amount, ct);
    }

    public async Task<Dictionary<int, decimal>> GetWalletBalancesForUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.WalletAllocations
            .Include(a => a.Wallet)
            .Where(a => a.Wallet.UserId == userId && !a.IsDeleted && !a.Wallet.IsDeleted)
            .GroupBy(a => a.WalletId)
            .Select(g => new { WalletId = g.Key, Balance = g.Sum(a => a.Amount) })
            .ToDictionaryAsync(x => x.WalletId, x => x.Balance, ct);
    }
}
