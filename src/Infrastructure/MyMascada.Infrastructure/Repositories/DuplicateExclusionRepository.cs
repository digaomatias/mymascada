using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class DuplicateExclusionRepository : IDuplicateExclusionRepository
{
    private readonly ApplicationDbContext _context;

    public DuplicateExclusionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DuplicateExclusion>> GetByUserIdAsync(Guid userId)
    {
        return await _context.DuplicateExclusions
            .Where(de => de.UserId == userId)
            .OrderByDescending(de => de.ExcludedAt)
            .ToListAsync();
    }

    public async Task<bool> IsExcludedAsync(Guid userId, IEnumerable<int> transactionIds)
    {
        var sortedIds = string.Join(",", transactionIds.OrderBy(id => id));
        
        return await _context.DuplicateExclusions
            .AnyAsync(de => de.UserId == userId && de.TransactionIds == sortedIds);
    }

    public async Task<DuplicateExclusion> AddAsync(DuplicateExclusion exclusion)
    {
        _context.DuplicateExclusions.Add(exclusion);
        await _context.SaveChangesAsync();
        return exclusion;
    }

    public async Task<List<DuplicateExclusion>> GetApplicableExclusionsAsync(Guid userId, IEnumerable<int> transactionIds)
    {
        var exclusions = await _context.DuplicateExclusions
            .Where(de => de.UserId == userId)
            .ToListAsync();

        // Filter in memory to use the AppliesToTransactions method
        return exclusions
            .Where(exclusion => exclusion.AppliesToTransactions(transactionIds))
            .ToList();
    }
}