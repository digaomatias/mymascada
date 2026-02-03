using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class ReconciliationItemRepository : IReconciliationItemRepository
{
    private readonly ApplicationDbContext _context;

    public ReconciliationItemRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReconciliationItem?> GetByIdAsync(int id, Guid userId)
    {
        return await _context.ReconciliationItems
            .Include(ri => ri.Reconciliation)
                .ThenInclude(r => r.Account)
            .Include(ri => ri.Transaction)
                .ThenInclude(t => t.Category)
            .FirstOrDefaultAsync(ri => ri.Id == id && 
                                      ri.Reconciliation.Account.UserId == userId &&
                                      !ri.IsDeleted);
    }

    public async Task<IEnumerable<ReconciliationItem>> GetByReconciliationIdAsync(int reconciliationId, Guid userId)
    {
        return await _context.ReconciliationItems
            .Include(ri => ri.Transaction)
                .ThenInclude(t => t.Category)
            .Include(ri => ri.Reconciliation)
                .ThenInclude(r => r.Account)
            .Where(ri => ri.ReconciliationId == reconciliationId && 
                        ri.Reconciliation.Account.UserId == userId &&
                        !ri.IsDeleted)
            .OrderBy(ri => ri.ItemType)
            .ThenByDescending(ri => ri.MatchConfidence)
            .ToListAsync();
    }

    public async Task<IEnumerable<ReconciliationItem>> GetByTransactionIdAsync(int transactionId, Guid userId)
    {
        return await _context.ReconciliationItems
            .Include(ri => ri.Reconciliation)
                .ThenInclude(r => r.Account)
            .Include(ri => ri.Transaction)
            .Where(ri => ri.TransactionId == transactionId && 
                        ri.Reconciliation.Account.UserId == userId &&
                        !ri.IsDeleted)
            .ToListAsync();
    }

    public async Task<(IEnumerable<ReconciliationItem> items, int totalCount)> GetFilteredAsync(
        Guid userId,
        int reconciliationId,
        int page = 1,
        int pageSize = 25,
        ReconciliationItemType? itemType = null,
        decimal? minConfidence = null,
        MatchMethod? matchMethod = null,
        string sortBy = "CreatedAt",
        string sortDirection = "desc")
    {
        var query = _context.ReconciliationItems
            .Include(ri => ri.Transaction)
                .ThenInclude(t => t.Category)
            .Include(ri => ri.Reconciliation)
                .ThenInclude(r => r.Account)
            .Where(ri => ri.ReconciliationId == reconciliationId && 
                        ri.Reconciliation.Account.UserId == userId &&
                        !ri.IsDeleted);

        // Apply filters
        if (itemType.HasValue)
            query = query.Where(ri => ri.ItemType == itemType.Value);

        if (minConfidence.HasValue)
            query = query.Where(ri => ri.MatchConfidence >= minConfidence.Value);

        if (matchMethod.HasValue)
            query = query.Where(ri => ri.MatchMethod == matchMethod.Value);

        // Apply sorting
        query = sortBy.ToLower() switch
        {
            "itemtype" => sortDirection.ToLower() == "asc"
                ? query.OrderBy(ri => ri.ItemType)
                : query.OrderByDescending(ri => ri.ItemType),
            "matchconfidence" => sortDirection.ToLower() == "asc"
                ? query.OrderBy(ri => ri.MatchConfidence)
                : query.OrderByDescending(ri => ri.MatchConfidence),
            "matchmethod" => sortDirection.ToLower() == "asc"
                ? query.OrderBy(ri => ri.MatchMethod)
                : query.OrderByDescending(ri => ri.MatchMethod),
            _ => sortDirection.ToLower() == "asc"
                ? query.OrderBy(ri => ri.CreatedAt)
                : query.OrderByDescending(ri => ri.CreatedAt)
        };

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<ReconciliationItem> AddAsync(ReconciliationItem item)
    {
        item.CreatedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        
        _context.ReconciliationItems.Add(item);
        await _context.SaveChangesAsync();
        return item;
    }

    public async Task<IEnumerable<ReconciliationItem>> AddRangeAsync(IEnumerable<ReconciliationItem> items)
    {
        var itemsList = items.ToList();
        var now = DateTime.UtcNow;
        
        foreach (var item in itemsList)
        {
            item.CreatedAt = now;
            item.UpdatedAt = now;
        }
        
        _context.ReconciliationItems.AddRange(itemsList);
        await _context.SaveChangesAsync();
        return itemsList;
    }

    public async Task UpdateAsync(ReconciliationItem item)
    {
        item.UpdatedAt = DateTime.UtcNow;
        _context.ReconciliationItems.Update(item);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(ReconciliationItem item)
    {
        item.IsDeleted = true;
        item.DeletedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        
        _context.ReconciliationItems.Update(item);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteByReconciliationIdAsync(int reconciliationId, Guid userId)
    {
        var items = await _context.ReconciliationItems
            .Include(ri => ri.Reconciliation)
                .ThenInclude(r => r.Account)
            .Where(ri => ri.ReconciliationId == reconciliationId && 
                        ri.Reconciliation.Account.UserId == userId &&
                        !ri.IsDeleted)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            item.IsDeleted = true;
            item.DeletedAt = now;
            item.UpdatedAt = now;
        }

        _context.ReconciliationItems.UpdateRange(items);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsTransactionReconciledAsync(int transactionId, Guid userId)
    {
        return await _context.ReconciliationItems
            .Include(ri => ri.Reconciliation)
                .ThenInclude(r => r.Account)
            .AnyAsync(ri => ri.TransactionId == transactionId && 
                           ri.Reconciliation.Account.UserId == userId &&
                           ri.Reconciliation.Status == ReconciliationStatus.Completed &&
                           !ri.IsDeleted);
    }

    public async Task<ReconciliationItem?> GetByTransactionAndReconciliationAsync(int transactionId, int reconciliationId, Guid userId)
    {
        return await _context.ReconciliationItems
            .Include(ri => ri.Reconciliation)
                .ThenInclude(r => r.Account)
            .Include(ri => ri.Transaction)
            .FirstOrDefaultAsync(ri => ri.TransactionId == transactionId && 
                                      ri.ReconciliationId == reconciliationId &&
                                      ri.Reconciliation.Account.UserId == userId &&
                                      !ri.IsDeleted);
    }

    public async Task<int> GetUnmatchedCountAsync(int reconciliationId, Guid userId)
    {
        return await _context.ReconciliationItems
            .Include(ri => ri.Reconciliation)
                .ThenInclude(r => r.Account)
            .CountAsync(ri => ri.ReconciliationId == reconciliationId && 
                             ri.Reconciliation.Account.UserId == userId &&
                             (ri.ItemType == ReconciliationItemType.UnmatchedApp || ri.ItemType == ReconciliationItemType.UnmatchedBank) &&
                             !ri.IsDeleted);
    }

    public async Task<int> GetMatchedCountAsync(int reconciliationId, Guid userId)
    {
        return await _context.ReconciliationItems
            .Include(ri => ri.Reconciliation)
                .ThenInclude(r => r.Account)
            .CountAsync(ri => ri.ReconciliationId == reconciliationId && 
                             ri.Reconciliation.Account.UserId == userId &&
                             ri.ItemType == ReconciliationItemType.Matched &&
                             !ri.IsDeleted);
    }

    public async Task<decimal> GetMatchedPercentageAsync(int reconciliationId, Guid userId)
    {
        var totalCount = await _context.ReconciliationItems
            .Include(ri => ri.Reconciliation)
                .ThenInclude(r => r.Account)
            .CountAsync(ri => ri.ReconciliationId == reconciliationId && 
                             ri.Reconciliation.Account.UserId == userId &&
                             !ri.IsDeleted);

        if (totalCount == 0)
            return 0;

        var matchedCount = await GetMatchedCountAsync(reconciliationId, userId);
        return (decimal)matchedCount / totalCount * 100;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}