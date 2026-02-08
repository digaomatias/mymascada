using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class TransferRepository : ITransferRepository
{
    private readonly ApplicationDbContext _context;
    private readonly IAccountAccessService _accountAccess;

    public TransferRepository(ApplicationDbContext context, IAccountAccessService accountAccess)
    {
        _context = context;
        _accountAccess = accountAccess;
    }

    public async Task<Transfer?> GetByIdAsync(int id, Guid userId)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        return await _context.Transfers
            .Include(t => t.SourceAccount)
            .Include(t => t.DestinationAccount)
            .Include(t => t.Transactions)
            .FirstOrDefaultAsync(t => t.Id == id &&
                (accessibleIds.Contains(t.SourceAccountId) || accessibleIds.Contains(t.DestinationAccountId)));
    }

    public async Task<Transfer?> GetByIdAsync(Guid transferId, Guid userId)
    {
        return await GetByTransferIdAsync(transferId, userId);
    }

    public async Task<Transfer?> GetByTransferIdAsync(Guid transferId, Guid userId)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        return await _context.Transfers
            .Include(t => t.SourceAccount)
            .Include(t => t.DestinationAccount)
            .Include(t => t.Transactions)
            .FirstOrDefaultAsync(t => t.TransferId == transferId &&
                (accessibleIds.Contains(t.SourceAccountId) || accessibleIds.Contains(t.DestinationAccountId)));
    }

    public async Task<IEnumerable<Transfer>> GetByUserIdAsync(Guid userId)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        return await _context.Transfers
            .Include(t => t.SourceAccount)
            .Include(t => t.DestinationAccount)
            .Where(t => accessibleIds.Contains(t.SourceAccountId) || accessibleIds.Contains(t.DestinationAccountId))
            .OrderByDescending(t => t.TransferDate)
            .ToListAsync();
    }

    public async Task<(IEnumerable<Transfer> transfers, int totalCount)> GetFilteredAsync(
        Guid userId,
        int page = 1,
        int pageSize = 50,
        int? sourceAccountId = null,
        int? destinationAccountId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        decimal? minAmount = null,
        decimal? maxAmount = null,
        TransferStatus? status = null,
        string sortBy = "TransferDate",
        string sortDirection = "desc")
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        var query = _context.Transfers
            .Include(t => t.SourceAccount)
            .Include(t => t.DestinationAccount)
            .Where(t => accessibleIds.Contains(t.SourceAccountId) || accessibleIds.Contains(t.DestinationAccountId));

        // Apply filters
        if (sourceAccountId.HasValue)
            query = query.Where(t => t.SourceAccountId == sourceAccountId.Value);

        if (destinationAccountId.HasValue)
            query = query.Where(t => t.DestinationAccountId == destinationAccountId.Value);

        if (startDate.HasValue)
            query = query.Where(t => t.TransferDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(t => t.TransferDate <= endDate.Value);

        if (minAmount.HasValue)
            query = query.Where(t => t.Amount >= minAmount.Value);

        if (maxAmount.HasValue)
            query = query.Where(t => t.Amount <= maxAmount.Value);

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting
        query = sortBy.ToLower() switch
        {
            "amount" => sortDirection.ToLower() == "asc"
                ? query.OrderBy(t => t.Amount)
                : query.OrderByDescending(t => t.Amount),
            "status" => sortDirection.ToLower() == "asc"
                ? query.OrderBy(t => t.Status)
                : query.OrderByDescending(t => t.Status),
            _ => sortDirection.ToLower() == "asc"
                ? query.OrderBy(t => t.TransferDate)
                : query.OrderByDescending(t => t.TransferDate)
        };

        // Apply pagination
        var transfers = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (transfers, totalCount);
    }

    public Task<Transfer> AddAsync(Transfer transfer)
    {
        _context.Transfers.Add(transfer);
        return Task.FromResult(transfer);
    }

    public Task UpdateAsync(Transfer transfer)
    {
        _context.Transfers.Update(transfer);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Transfer transfer)
    {
        _context.Transfers.Remove(transfer);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsByTransferIdAsync(Guid transferId)
    {
        return await _context.Transfers
            .AnyAsync(t => t.TransferId == transferId);
    }

    public async Task<IEnumerable<Transfer>> GetRecentAsync(Guid userId, int count = 10)
    {
        var accessibleIds = await _accountAccess.GetAccessibleAccountIdsAsync(userId);
        return await _context.Transfers
            .Include(t => t.SourceAccount)
            .Include(t => t.DestinationAccount)
            .Where(t => accessibleIds.Contains(t.SourceAccountId) || accessibleIds.Contains(t.DestinationAccountId))
            .OrderByDescending(t => t.TransferDate)
            .Take(count)
            .ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
