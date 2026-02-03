using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class ReconciliationAuditLogRepository : IReconciliationAuditLogRepository
{
    private readonly ApplicationDbContext _context;

    public ReconciliationAuditLogRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReconciliationAuditLog?> GetByIdAsync(int id, Guid userId)
    {
        return await _context.ReconciliationAuditLogs
            .Include(ral => ral.Reconciliation)
                .ThenInclude(r => r.Account)
            .FirstOrDefaultAsync(ral => ral.Id == id && 
                                       ral.Reconciliation.Account.UserId == userId &&
                                       !ral.IsDeleted);
    }

    public async Task<IEnumerable<ReconciliationAuditLog>> GetByReconciliationIdAsync(int reconciliationId, Guid userId)
    {
        return await _context.ReconciliationAuditLogs
            .Include(ral => ral.Reconciliation)
                .ThenInclude(r => r.Account)
            .Where(ral => ral.ReconciliationId == reconciliationId && 
                         ral.Reconciliation.Account.UserId == userId &&
                         !ral.IsDeleted)
            .OrderByDescending(ral => ral.Timestamp)
            .ToListAsync();
    }

    public async Task<(IEnumerable<ReconciliationAuditLog> logs, int totalCount)> GetFilteredAsync(
        Guid userId,
        int? reconciliationId = null,
        ReconciliationAction? action = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 25,
        string sortBy = "Timestamp",
        string sortDirection = "desc")
    {
        var query = _context.ReconciliationAuditLogs
            .Include(ral => ral.Reconciliation)
                .ThenInclude(r => r.Account)
            .Where(ral => ral.Reconciliation.Account.UserId == userId && !ral.IsDeleted);

        // Apply filters
        if (reconciliationId.HasValue)
            query = query.Where(ral => ral.ReconciliationId == reconciliationId.Value);

        if (action.HasValue)
            query = query.Where(ral => ral.Action == action.Value);

        if (startDate.HasValue)
            query = query.Where(ral => ral.Timestamp >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(ral => ral.Timestamp <= endDate.Value);

        // Apply sorting
        query = sortBy.ToLower() switch
        {
            "action" => sortDirection.ToLower() == "asc"
                ? query.OrderBy(ral => ral.Action)
                : query.OrderByDescending(ral => ral.Action),
            "userid" => sortDirection.ToLower() == "asc"
                ? query.OrderBy(ral => ral.UserId)
                : query.OrderByDescending(ral => ral.UserId),
            "reconciliationid" => sortDirection.ToLower() == "asc"
                ? query.OrderBy(ral => ral.ReconciliationId)
                : query.OrderByDescending(ral => ral.ReconciliationId),
            _ => sortDirection.ToLower() == "asc"
                ? query.OrderBy(ral => ral.Timestamp)
                : query.OrderByDescending(ral => ral.Timestamp)
        };

        var totalCount = await query.CountAsync();

        var logs = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (logs, totalCount);
    }

    public async Task<ReconciliationAuditLog> AddAsync(ReconciliationAuditLog log)
    {
        log.CreatedAt = DateTime.UtcNow;
        log.UpdatedAt = DateTime.UtcNow;
        log.Timestamp = DateTime.UtcNow;
        
        _context.ReconciliationAuditLogs.Add(log);
        await _context.SaveChangesAsync();
        return log;
    }

    public async Task<IEnumerable<ReconciliationAuditLog>> AddRangeAsync(IEnumerable<ReconciliationAuditLog> logs)
    {
        var logsList = logs.ToList();
        var now = DateTime.UtcNow;
        
        foreach (var log in logsList)
        {
            log.CreatedAt = now;
            log.UpdatedAt = now;
            log.Timestamp = now;
        }
        
        _context.ReconciliationAuditLogs.AddRange(logsList);
        await _context.SaveChangesAsync();
        return logsList;
    }

    public async Task<IEnumerable<ReconciliationAuditLog>> GetRecentByUserAsync(Guid userId, int count = 10)
    {
        return await _context.ReconciliationAuditLogs
            .Include(ral => ral.Reconciliation)
                .ThenInclude(r => r.Account)
            .Where(ral => ral.Reconciliation.Account.UserId == userId && !ral.IsDeleted)
            .OrderByDescending(ral => ral.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    public async Task<int> GetCountByReconciliationAsync(int reconciliationId, Guid userId)
    {
        return await _context.ReconciliationAuditLogs
            .Include(ral => ral.Reconciliation)
                .ThenInclude(r => r.Account)
            .CountAsync(ral => ral.ReconciliationId == reconciliationId && 
                              ral.Reconciliation.Account.UserId == userId &&
                              !ral.IsDeleted);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}