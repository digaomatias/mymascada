using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class ReconciliationRepository : IReconciliationRepository
{
    private readonly ApplicationDbContext _context;

    public ReconciliationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Reconciliation?> GetByIdAsync(int id, Guid userId)
    {
        return await _context.Reconciliations
            .Include(r => r.Account)
            .Include(r => r.ReconciliationItems)
            .FirstOrDefaultAsync(r => r.Id == id && 
                                     r.Account.UserId == userId &&
                                     !r.IsDeleted);
    }

    public async Task<IEnumerable<Reconciliation>> GetByAccountIdAsync(int accountId, Guid userId)
    {
        return await _context.Reconciliations
            .Include(r => r.Account)
            .Where(r => r.AccountId == accountId && 
                       r.Account.UserId == userId &&
                       !r.IsDeleted)
            .OrderByDescending(r => r.ReconciliationDate)
            .ToListAsync();
    }

    public async Task<(IEnumerable<Reconciliation> reconciliations, int totalCount)> GetFilteredAsync(
        Guid userId,
        int page = 1,
        int pageSize = 25,
        int? accountId = null,
        ReconciliationStatus? status = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string sortBy = "ReconciliationDate",
        string sortDirection = "desc")
    {
        var query = _context.Reconciliations
            .Include(r => r.Account)
            .Where(r => r.Account.UserId == userId && !r.IsDeleted);

        // Apply filters
        if (accountId.HasValue)
            query = query.Where(r => r.AccountId == accountId.Value);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        if (startDate.HasValue)
            query = query.Where(r => r.ReconciliationDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(r => r.ReconciliationDate <= endDate.Value);

        // Apply sorting
        query = sortBy.ToLower() switch
        {
            "statementenddate" => sortDirection.ToLower() == "asc" 
                ? query.OrderBy(r => r.StatementEndDate)
                : query.OrderByDescending(r => r.StatementEndDate),
            "status" => sortDirection.ToLower() == "asc"
                ? query.OrderBy(r => r.Status)
                : query.OrderByDescending(r => r.Status),
            "statementendbalance" => sortDirection.ToLower() == "asc"
                ? query.OrderBy(r => r.StatementEndBalance)
                : query.OrderByDescending(r => r.StatementEndBalance),
            _ => sortDirection.ToLower() == "asc"
                ? query.OrderBy(r => r.ReconciliationDate)
                : query.OrderByDescending(r => r.ReconciliationDate)
        };

        var totalCount = await query.CountAsync();

        var reconciliations = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (reconciliations, totalCount);
    }

    public async Task<Reconciliation> AddAsync(Reconciliation reconciliation)
    {
        reconciliation.CreatedAt = DateTime.UtcNow;
        reconciliation.UpdatedAt = DateTime.UtcNow;
        
        _context.Reconciliations.Add(reconciliation);
        await _context.SaveChangesAsync();
        return reconciliation;
    }

    public async Task UpdateAsync(Reconciliation reconciliation)
    {
        reconciliation.UpdatedAt = DateTime.UtcNow;
        _context.Reconciliations.Update(reconciliation);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Reconciliation reconciliation)
    {
        reconciliation.IsDeleted = true;
        reconciliation.DeletedAt = DateTime.UtcNow;
        reconciliation.UpdatedAt = DateTime.UtcNow;
        
        _context.Reconciliations.Update(reconciliation);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(int id, Guid userId)
    {
        return await _context.Reconciliations
            .AnyAsync(r => r.Id == id && 
                          r.Account.UserId == userId &&
                          !r.IsDeleted);
    }

    public async Task<Reconciliation?> GetLatestByAccountAsync(int accountId, Guid userId)
    {
        return await _context.Reconciliations
            .Include(r => r.Account)
            .Where(r => r.AccountId == accountId && 
                       r.Account.UserId == userId &&
                       !r.IsDeleted)
            .OrderByDescending(r => r.ReconciliationDate)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Reconciliation>> GetRecentAsync(Guid userId, int count = 10)
    {
        return await _context.Reconciliations
            .Include(r => r.Account)
            .Where(r => r.Account.UserId == userId && !r.IsDeleted)
            .OrderByDescending(r => r.ReconciliationDate)
            .Take(count)
            .ToListAsync();
    }

    public async Task<int> GetCountByAccountAsync(int accountId, Guid userId)
    {
        return await _context.Reconciliations
            .CountAsync(r => r.AccountId == accountId && 
                            r.Account.UserId == userId &&
                            !r.IsDeleted);
    }

    public async Task<decimal?> GetLastReconciledBalanceAsync(int accountId, Guid userId)
    {
        var lastReconciliation = await _context.Reconciliations
            .Where(r => r.AccountId == accountId && 
                       r.Account.UserId == userId &&
                       r.Status == ReconciliationStatus.Completed &&
                       !r.IsDeleted)
            .OrderByDescending(r => r.ReconciliationDate)
            .FirstOrDefaultAsync();

        return lastReconciliation?.StatementEndBalance;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}