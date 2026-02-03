using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for managing bank connections.
/// </summary>
public class BankConnectionRepository : IBankConnectionRepository
{
    private readonly ApplicationDbContext _context;

    public BankConnectionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<BankConnection?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.BankConnections
            .Include(bc => bc.Account)
            .FirstOrDefaultAsync(bc => bc.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<BankConnection?> GetByAccountIdAsync(int accountId, CancellationToken ct = default)
    {
        return await _context.BankConnections
            .Include(bc => bc.Account)
            .FirstOrDefaultAsync(bc => bc.AccountId == accountId, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BankConnection>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.BankConnections
            .Include(bc => bc.Account)
            .Where(bc => bc.UserId == userId)
            .OrderBy(bc => bc.Account.Name)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BankConnection>> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.BankConnections
            .Include(bc => bc.Account)
            .Where(bc => bc.UserId == userId && bc.IsActive)
            .OrderBy(bc => bc.Account.Name)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BankConnection> AddAsync(BankConnection bankConnection, CancellationToken ct = default)
    {
        await _context.BankConnections.AddAsync(bankConnection, ct);
        await _context.SaveChangesAsync(ct);
        return bankConnection;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(BankConnection bankConnection, CancellationToken ct = default)
    {
        _context.BankConnections.Update(bankConnection);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var bankConnection = await _context.BankConnections
            .FirstOrDefaultAsync(bc => bc.Id == id, ct);

        if (bankConnection != null)
        {
            // Soft delete following the pattern from other repositories
            bankConnection.IsDeleted = true;
            bankConnection.DeletedAt = DateTime.UtcNow;
            _context.BankConnections.Update(bankConnection);
            await _context.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByExternalAccountIdAsync(string externalAccountId, string providerId, CancellationToken ct = default)
    {
        return await _context.BankConnections
            .AnyAsync(bc => bc.ExternalAccountId == externalAccountId && bc.ProviderId == providerId, ct);
    }

    /// <inheritdoc />
    public async Task<BankConnection?> GetByExternalAccountIdAsync(string externalAccountId, string providerId, CancellationToken ct = default)
    {
        return await _context.BankConnections
            .Include(bc => bc.Account)
            .FirstOrDefaultAsync(bc => bc.ExternalAccountId == externalAccountId && bc.ProviderId == providerId, ct);
    }
}
