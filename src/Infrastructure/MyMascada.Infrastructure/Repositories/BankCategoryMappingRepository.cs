using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for BankCategoryMapping data access.
/// </summary>
public class BankCategoryMappingRepository : IBankCategoryMappingRepository
{
    private readonly ApplicationDbContext _context;

    public BankCategoryMappingRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BankCategoryMapping?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.BankCategoryMappings
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<BankCategoryMapping?> GetByIdAsync(int id, Guid userId, CancellationToken ct = default)
    {
        return await _context.BankCategoryMappings
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId, ct);
    }

    public async Task<BankCategoryMapping?> GetByBankCategoryAsync(
        string normalizedBankCategoryName,
        string providerId,
        Guid userId,
        CancellationToken ct = default)
    {
        return await _context.BankCategoryMappings
            .Include(m => m.Category)
            .FirstOrDefaultAsync(m =>
                m.NormalizedName == normalizedBankCategoryName &&
                m.ProviderId == providerId &&
                m.UserId == userId &&
                m.IsActive, ct);
    }

    public async Task<IEnumerable<BankCategoryMapping>> GetByUserIdAsync(
        Guid userId,
        string? providerId = null,
        bool activeOnly = true,
        CancellationToken ct = default)
    {
        var query = _context.BankCategoryMappings
            .Include(m => m.Category)
            .Where(m => m.UserId == userId);

        if (!string.IsNullOrEmpty(providerId))
        {
            query = query.Where(m => m.ProviderId == providerId);
        }

        if (activeOnly)
        {
            query = query.Where(m => m.IsActive);
        }

        return await query
            .OrderBy(m => m.BankCategoryName)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<BankCategoryMapping>> GetBatchByBankCategoriesAsync(
        IEnumerable<string> normalizedBankCategoryNames,
        string providerId,
        Guid userId,
        CancellationToken ct = default)
    {
        var nameList = normalizedBankCategoryNames.ToList();

        return await _context.BankCategoryMappings
            .Include(m => m.Category)
            .Where(m =>
                nameList.Contains(m.NormalizedName) &&
                m.ProviderId == providerId &&
                m.UserId == userId &&
                m.IsActive)
            .ToListAsync(ct);
    }

    public async Task<BankCategoryMapping> AddAsync(BankCategoryMapping mapping, CancellationToken ct = default)
    {
        await _context.BankCategoryMappings.AddAsync(mapping, ct);
        await _context.SaveChangesAsync(ct);
        return mapping;
    }

    public async Task<IEnumerable<BankCategoryMapping>> AddRangeAsync(
        IEnumerable<BankCategoryMapping> mappings,
        CancellationToken ct = default)
    {
        var mappingList = mappings.ToList();
        await _context.BankCategoryMappings.AddRangeAsync(mappingList, ct);
        await _context.SaveChangesAsync(ct);
        return mappingList;
    }

    public async Task UpdateAsync(BankCategoryMapping mapping, CancellationToken ct = default)
    {
        _context.BankCategoryMappings.Update(mapping);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(BankCategoryMapping mapping, CancellationToken ct = default)
    {
        mapping.IsDeleted = true;
        mapping.DeletedAt = DateTime.UtcNow;
        mapping.IsActive = false;
        _context.BankCategoryMappings.Update(mapping);
        await _context.SaveChangesAsync(ct);
    }

    public async Task IncrementApplicationCountAsync(int mappingId, CancellationToken ct = default)
    {
        var mapping = await _context.BankCategoryMappings.FindAsync(new object[] { mappingId }, ct);
        if (mapping != null)
        {
            mapping.ApplicationCount++;
            mapping.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task IncrementOverrideCountAsync(int mappingId, CancellationToken ct = default)
    {
        var mapping = await _context.BankCategoryMappings.FindAsync(new object[] { mappingId }, ct);
        if (mapping != null)
        {
            mapping.OverrideCount++;
            mapping.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<IEnumerable<BankCategoryMapping>> GetByCategoryIdAsync(
        int categoryId,
        CancellationToken ct = default)
    {
        return await _context.BankCategoryMappings
            .Where(m => m.CategoryId == categoryId && m.IsActive)
            .ToListAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
