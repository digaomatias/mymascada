using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class WaitlistRepository : IWaitlistRepository
{
    private readonly ApplicationDbContext _context;

    public WaitlistRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(WaitlistEntry entry)
    {
        await _context.WaitlistEntries.AddAsync(entry);
        await _context.SaveChangesAsync();
    }

    public async Task<WaitlistEntry?> GetByNormalizedEmailAsync(string normalizedEmail)
    {
        return await _context.WaitlistEntries
            .FirstOrDefaultAsync(e => e.NormalizedEmail == normalizedEmail);
    }

    public async Task<WaitlistEntry?> GetByIdAsync(Guid id)
    {
        return await _context.WaitlistEntries.FindAsync(id);
    }

    public async Task<(IReadOnlyList<WaitlistEntry> Items, int TotalCount)> GetPagedAsync(WaitlistStatus? status, int page, int pageSize)
    {
        var query = _context.WaitlistEntries.AsQueryable();
        if (status.HasValue)
            query = query.Where(e => e.Status == status.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task UpdateAsync(WaitlistEntry entry)
    {
        _context.WaitlistEntries.Update(entry);
        await _context.SaveChangesAsync();
    }
}
