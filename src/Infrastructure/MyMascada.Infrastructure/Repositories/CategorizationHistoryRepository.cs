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
            .AsNoTracking()
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
            .OrderByDescending(h => h.LastUsedAt)
            .Take(1000)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Upserts a categorization history entry. Does NOT call SaveChangesAsync — the caller
    /// is responsible for flushing changes, enabling batching of multiple upserts.
    /// Concurrent unique-constraint violations are handled by the caller's error handling
    /// (history recording is best-effort and non-critical).
    /// </summary>
    public async Task<CategorizationHistory> UpsertAsync(
        Guid userId,
        string normalizedDescription,
        string originalDescription,
        int categoryId,
        CategorizationHistorySource source,
        CancellationToken ct = default)
    {
        // Check Local (in-memory tracked entities) first to avoid duplicate tracking within a batch.
        // Use IgnoreQueryFilters to include soft-deleted rows — the unique index covers all rows.
        var existing = _context.CategorizationHistories.Local
            .FirstOrDefault(h => h.UserId == userId && h.NormalizedDescription == normalizedDescription)
            ?? await _context.CategorizationHistories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(h => h.UserId == userId && h.NormalizedDescription == normalizedDescription, ct);

        if (existing != null)
        {
            // Revive soft-deleted rows so they become visible to normal queries again
            if (existing.IsDeleted)
            {
                existing.IsDeleted = false;
                existing.DeletedAt = null;
            }

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

        return existing;
    }

    /// <summary>
    /// Upserts with an absolute count (used by backfill). Does NOT call SaveChangesAsync.
    /// </summary>
    public async Task<CategorizationHistory> UpsertWithAbsoluteCountAsync(
        Guid userId,
        string normalizedDescription,
        string originalDescription,
        int categoryId,
        int count,
        CategorizationHistorySource source,
        CancellationToken ct = default)
    {
        // Check Local first, then DB with IgnoreQueryFilters to include soft-deleted rows
        var existing = _context.CategorizationHistories.Local
            .FirstOrDefault(h => h.UserId == userId && h.NormalizedDescription == normalizedDescription)
            ?? await _context.CategorizationHistories
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(h => h.UserId == userId && h.NormalizedDescription == normalizedDescription, ct);

        if (existing != null)
        {
            // Revive soft-deleted rows so they become visible to normal queries again
            if (existing.IsDeleted)
            {
                existing.IsDeleted = false;
                existing.DeletedAt = null;
            }

            if (existing.CategoryId != categoryId)
            {
                // Category changed — use the backfill count for the new category
                existing.CategoryId = categoryId;
                existing.MatchCount = count;
                existing.Source = source;
            }
            else
            {
                // Same category — idempotent: take the max to avoid inflation on reruns
                existing.MatchCount = Math.Max(existing.MatchCount, count);
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
                MatchCount = count,
                LastUsedAt = DateTime.UtcNow,
                Source = source
            };
            _context.CategorizationHistories.Add(existing);
        }

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
