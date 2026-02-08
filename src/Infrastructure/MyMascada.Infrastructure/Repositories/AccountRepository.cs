using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly ApplicationDbContext _context;
    private readonly IAccountAccessService _accountAccess;

    public AccountRepository(ApplicationDbContext context, IAccountAccessService accountAccess)
    {
        _context = context;
        _accountAccess = accountAccess;
    }

    public async Task<Account?> GetByIdAsync(int id, Guid userId)
    {
        if (!await _accountAccess.CanAccessAccountAsync(userId, id))
            return null;

        return await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
    }

    public async Task<IEnumerable<Account>> GetByUserIdAsync(Guid userId)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);

        return await _context.Accounts
            .Where(a => accessibleIds.Contains(a.Id) && !a.IsDeleted)
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    public async Task<Account> AddAsync(Account account)
    {
        await _context.Accounts.AddAsync(account);
        await _context.SaveChangesAsync();
        return account;
    }

    public async Task UpdateAsync(Account account)
    {
        _context.Accounts.Update(account);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Account account)
    {
        account.IsDeleted = true;
        account.DeletedAt = DateTime.UtcNow;
        _context.Accounts.Update(account);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(int id, Guid userId)
    {
        return await _accountAccess.CanAccessAccountAsync(userId, id);
    }

    // Data integrity methods - owner-only operations
    public async Task<IEnumerable<Account>> GetSoftDeletedAccountsAsync(Guid userId)
    {
        return await _context.Accounts
            .IgnoreQueryFilters()
            .Where(a => a.UserId == userId && a.IsDeleted)
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Account>> GetSoftDeletedAccountsWithTransactionsAsync(Guid userId)
    {
        return await _context.Accounts
            .IgnoreQueryFilters()
            .Where(a => a.UserId == userId &&
                       a.IsDeleted &&
                       a.Transactions.Any(t => !t.IsDeleted))
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    public async Task RestoreAccountAsync(int accountId, Guid userId)
    {
        var account = await _context.Accounts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId && a.IsDeleted);

        if (account != null)
        {
            account.IsDeleted = false;
            account.DeletedAt = null;
            account.UpdatedAt = DateTime.UtcNow;

            _context.Accounts.Update(account);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Account?> GetByIdIncludingDeletedAsync(int id, Guid userId)
    {
        return await _context.Accounts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
    }
}
