using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

public class CategorizationHistoryRepository : ICategorizationHistoryRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CategorizationHistoryRepository> _logger;
    private const int MaxRetries = 3;

    public CategorizationHistoryRepository(
        ApplicationDbContext context,
        ILogger<CategorizationHistoryRepository> logger)
    {
        _context = context;
        _logger = logger;
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
        CategorizationHistorySource source,
        CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                return await UpsertCore(userId, normalizedDescription, originalDescription, categoryId, source, ct);
            }
            catch (DbUpdateException) when (attempt < MaxRetries - 1)
            {
                // Unique constraint violation from concurrent insert — reload and retry
                _logger.LogDebug(
                    "Unique constraint conflict on upsert (attempt {Attempt}), retrying",
                    attempt + 1);

                // Detach the conflicting tracked entity so we can re-query
                var tracked = _context.CategorizationHistories.Local
                    .FirstOrDefault(h => h.UserId == userId && h.NormalizedDescription == normalizedDescription);
                if (tracked != null)
                    _context.Entry(tracked).State = EntityState.Detached;
            }
        }

        // Should not reach here, but satisfy the compiler
        throw new InvalidOperationException("Upsert failed after maximum retries");
    }

    private async Task<CategorizationHistory> UpsertCore(
        Guid userId,
        string normalizedDescription,
        string originalDescription,
        int categoryId,
        CategorizationHistorySource source,
        CancellationToken ct)
    {
        // Check Local (in-memory tracked entities) first to avoid duplicate tracking within a batch
        var existing = _context.CategorizationHistories.Local
            .FirstOrDefault(h => h.UserId == userId && h.NormalizedDescription == normalizedDescription)
            ?? await _context.CategorizationHistories
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

        return existing;
    }

    public async Task<CategorizationHistory> UpsertWithAbsoluteCountAsync(
        Guid userId,
        string normalizedDescription,
        string originalDescription,
        int categoryId,
        int count,
        CategorizationHistorySource source,
        CancellationToken ct = default)
    {
        // Check Local first to avoid duplicate tracking within a batch
        var existing = _context.CategorizationHistories.Local
            .FirstOrDefault(h => h.UserId == userId && h.NormalizedDescription == normalizedDescription)
            ?? await _context.CategorizationHistories
                .FirstOrDefaultAsync(h => h.UserId == userId && h.NormalizedDescription == normalizedDescription, ct);

        if (existing != null)
        {
            // Idempotent: take the max to avoid inflation on reruns
            existing.MatchCount = Math.Max(existing.MatchCount, count);
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
