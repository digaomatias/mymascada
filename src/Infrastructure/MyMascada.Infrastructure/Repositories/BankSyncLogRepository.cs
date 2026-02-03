using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for managing bank sync logs.
/// </summary>
public class BankSyncLogRepository : IBankSyncLogRepository
{
    private readonly ApplicationDbContext _context;

    public BankSyncLogRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<BankSyncLog?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.BankSyncLogs
            .Include(sl => sl.BankConnection)
            .FirstOrDefaultAsync(sl => sl.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BankSyncLog>> GetByBankConnectionIdAsync(int bankConnectionId, int limit = 20, CancellationToken ct = default)
    {
        return await _context.BankSyncLogs
            .Include(sl => sl.BankConnection)
            .Where(sl => sl.BankConnectionId == bankConnectionId)
            .OrderByDescending(sl => sl.StartedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BankSyncLog?> GetLatestByBankConnectionIdAsync(int bankConnectionId, CancellationToken ct = default)
    {
        return await _context.BankSyncLogs
            .Include(sl => sl.BankConnection)
            .Where(sl => sl.BankConnectionId == bankConnectionId)
            .OrderByDescending(sl => sl.StartedAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BankSyncLog> AddAsync(BankSyncLog syncLog, CancellationToken ct = default)
    {
        await _context.BankSyncLogs.AddAsync(syncLog, ct);
        await _context.SaveChangesAsync(ct);
        return syncLog;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(BankSyncLog syncLog, CancellationToken ct = default)
    {
        _context.BankSyncLogs.Update(syncLog);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<(int TotalSyncs, int SuccessfulSyncs, int TotalTransactionsImported)> GetSyncStatisticsAsync(int bankConnectionId, CancellationToken ct = default)
    {
        var logs = await _context.BankSyncLogs
            .Where(sl => sl.BankConnectionId == bankConnectionId)
            .ToListAsync(ct);

        var totalSyncs = logs.Count;
        var successfulSyncs = logs.Count(sl => sl.Status == BankSyncStatus.Completed || sl.Status == BankSyncStatus.PartialSuccess);
        var totalTransactionsImported = logs.Sum(sl => sl.TransactionsImported);

        return (totalSyncs, successfulSyncs, totalTransactionsImported);
    }
}
