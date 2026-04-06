using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class CategorizationHistoryRepository : ICategorizationHistoryRepository
{
    private readonly ApplicationDbContext _context;

    public CategorizationHistoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CategorizationHistory?> FindByNormalizedDescriptionAsync(
        Guid userId, string normalizedDescription, CancellationToken ct = default)
    {
        return await _context.CategorizationHistories
            .Include(h => h.Category)
            .FirstOrDefaultAsync(h => h.UserId == userId && h.NormalizedDescription == normalizedDescription, ct);
    }

    public async Task<IReadOnlyList<CategorizationHistory>> GetAllForUserAsync(
        Guid userId, CancellationToken ct = default)
    {
        return await _context.CategorizationHistories
            .AsNoTracking()
            .Include(h => h.Category)
            .Where(h => h.UserId == userId)
            .ToListAsync(ct);
    }

    public async Task<CategorizationHistory> UpsertAsync(
        Guid userId,
        string normalizedDescription,
        string originalDescription,
        int categoryId,
        string source,
        CancellationToken ct = default)
    {
        var existing = await _context.CategorizationHistories
            .FirstOrDefaultAsync(h => h.UserId == userId && h.NormalizedDescription == normalizedDescription, ct);

        if (existing != null)
        {
            if (existing.CategoryId == categoryId)
            {
                // Same category — reinforce the mapping
                existing.MatchCount++;
            }
            else
            {
                // Different category — user changed their mind, reset
                existing.CategoryId = categoryId;
                existing.MatchCount = 1;
                existing.Source = source;
            }

            existing.LastUsedAt = DateTime.UtcNow;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new CategorizationHistory
            {
                UserId = userId,
                NormalizedDescription = normalizedDescription,
                OriginalDescription = originalDescription,
                CategoryId = categoryId,
                MatchCount = 1,
                LastUsedAt = DateTime.UtcNow,
                Source = source
            };
            _context.CategorizationHistories.Add(existing);
        }

        await _context.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<IReadOnlyList<Guid>> GetDistinctUserIdsWithCategorizedTransactionsAsync(CancellationToken ct = default)
    {
        return await _context.Transactions
            .AsNoTracking()
            .Where(t => t.CategoryId != null && !t.IsDeleted)
            .Select(t => t.Account.UserId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
