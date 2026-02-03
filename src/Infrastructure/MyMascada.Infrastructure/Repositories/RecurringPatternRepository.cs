using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

/// <summary>
/// Repository for managing recurring payment patterns and occurrences
/// </summary>
public class RecurringPatternRepository : IRecurringPatternRepository
{
    private readonly ApplicationDbContext _context;

    public RecurringPatternRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // Pattern CRUD operations

    /// <summary>
    /// Gets a recurring pattern by ID for a specific user
    /// </summary>
    public async Task<RecurringPattern?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.RecurringPatterns
            .Include(p => p.Category)
            .Include(p => p.Occurrences.OrderByDescending(o => o.ExpectedDate).Take(10))
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId && !p.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Gets all recurring patterns for a user
    /// </summary>
    public async Task<IEnumerable<RecurringPattern>> GetByUserIdAsync(
        Guid userId,
        bool includeOccurrences = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.RecurringPatterns
            .Include(p => p.Category)
            .Where(p => p.UserId == userId && !p.IsDeleted);

        if (includeOccurrences)
        {
            query = query.Include(p => p.Occurrences.OrderByDescending(o => o.ExpectedDate).Take(10));
        }

        return await query
            .OrderByDescending(p => p.Status == RecurringPatternStatus.Active)
            .ThenByDescending(p => p.Status == RecurringPatternStatus.AtRisk)
            .ThenBy(p => p.NextExpectedDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets recurring patterns by status for a user
    /// </summary>
    public async Task<IEnumerable<RecurringPattern>> GetByStatusAsync(
        Guid userId,
        RecurringPatternStatus status,
        CancellationToken cancellationToken = default)
    {
        return await _context.RecurringPatterns
            .Include(p => p.Category)
            .Where(p => p.UserId == userId && p.Status == status && !p.IsDeleted)
            .OrderBy(p => p.NextExpectedDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets active recurring patterns for a user (Active or AtRisk)
    /// </summary>
    public async Task<IEnumerable<RecurringPattern>> GetActiveAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.RecurringPatterns
            .Include(p => p.Category)
            .Where(p => p.UserId == userId
                        && (p.Status == RecurringPatternStatus.Active || p.Status == RecurringPatternStatus.AtRisk)
                        && !p.IsDeleted)
            .OrderBy(p => p.NextExpectedDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets a pattern by its normalized merchant key for a user
    /// </summary>
    public async Task<RecurringPattern?> GetByMerchantKeyAsync(
        Guid userId,
        string normalizedMerchantKey,
        CancellationToken cancellationToken = default)
    {
        return await _context.RecurringPatterns
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.UserId == userId
                                      && p.NormalizedMerchantKey == normalizedMerchantKey
                                      && !p.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Gets patterns with expected dates in a date range for a user
    /// </summary>
    public async Task<IEnumerable<RecurringPattern>> GetUpcomingAsync(
        Guid userId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.RecurringPatterns
            .Include(p => p.Category)
            .Where(p => p.UserId == userId
                        && (p.Status == RecurringPatternStatus.Active || p.Status == RecurringPatternStatus.AtRisk)
                        && p.NextExpectedDate >= fromDate
                        && p.NextExpectedDate <= toDate
                        && !p.IsDeleted)
            .OrderBy(p => p.NextExpectedDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets patterns where the grace window has expired (need status check)
    /// </summary>
    public async Task<IEnumerable<RecurringPattern>> GetPastDueAsync(
        Guid userId,
        DateTime currentDate,
        CancellationToken cancellationToken = default)
    {
        // Get all active/at-risk patterns where expected date has passed
        var patterns = await _context.RecurringPatterns
            .Include(p => p.Category)
            .Where(p => p.UserId == userId
                        && (p.Status == RecurringPatternStatus.Active || p.Status == RecurringPatternStatus.AtRisk)
                        && p.NextExpectedDate < currentDate
                        && !p.IsDeleted)
            .ToListAsync(cancellationToken);

        // Filter in memory for those past the grace window
        return patterns.Where(p => !p.IsWithinGraceWindow(currentDate)).ToList();
    }

    /// <summary>
    /// Creates a new recurring pattern
    /// </summary>
    public async Task<RecurringPattern> CreateAsync(RecurringPattern pattern, CancellationToken cancellationToken = default)
    {
        var entry = await _context.RecurringPatterns.AddAsync(pattern, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // Load navigation properties
        if (pattern.CategoryId.HasValue)
        {
            await _context.Entry(entry.Entity)
                .Reference(p => p.Category)
                .LoadAsync(cancellationToken);
        }

        return entry.Entity;
    }

    /// <summary>
    /// Updates an existing recurring pattern
    /// </summary>
    public async Task<RecurringPattern> UpdateAsync(RecurringPattern pattern, CancellationToken cancellationToken = default)
    {
        pattern.UpdatedAt = DateTime.UtcNow;
        _context.RecurringPatterns.Update(pattern);
        await _context.SaveChangesAsync(cancellationToken);
        return pattern;
    }

    /// <summary>
    /// Soft deletes a recurring pattern
    /// </summary>
    public async Task DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default)
    {
        var pattern = await _context.RecurringPatterns
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId && !p.IsDeleted, cancellationToken);

        if (pattern != null)
        {
            pattern.IsDeleted = true;
            pattern.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Upserts a pattern based on normalized merchant key (create or update)
    /// </summary>
    public async Task<RecurringPattern> UpsertAsync(RecurringPattern pattern, CancellationToken cancellationToken = default)
    {
        var existing = await GetByMerchantKeyAsync(pattern.UserId, pattern.NormalizedMerchantKey, cancellationToken);

        if (existing != null)
        {
            // Update existing pattern
            existing.MerchantName = pattern.MerchantName;
            existing.IntervalDays = pattern.IntervalDays;
            existing.AverageAmount = pattern.AverageAmount;
            existing.Confidence = pattern.Confidence;
            existing.NextExpectedDate = pattern.NextExpectedDate;
            existing.LastObservedAt = pattern.LastObservedAt;
            existing.OccurrenceCount = pattern.OccurrenceCount;
            existing.UpdatedAt = DateTime.UtcNow;

            // Only update status if pattern was cancelled and is being reactivated
            if (existing.Status == RecurringPatternStatus.Cancelled && pattern.Status == RecurringPatternStatus.Active)
            {
                existing.Status = RecurringPatternStatus.Active;
                existing.ConsecutiveMisses = 0;
            }

            return await UpdateAsync(existing, cancellationToken);
        }
        else
        {
            // Create new pattern
            return await CreateAsync(pattern, cancellationToken);
        }
    }

    // Occurrence operations

    /// <summary>
    /// Gets all occurrences for a pattern
    /// </summary>
    public async Task<IEnumerable<RecurringOccurrence>> GetOccurrencesAsync(
        int patternId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.RecurringOccurrences
            .Include(o => o.Transaction)
            .Where(o => o.PatternId == patternId && o.Pattern.UserId == userId && !o.IsDeleted)
            .OrderByDescending(o => o.ExpectedDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets recent occurrences for a pattern
    /// </summary>
    public async Task<IEnumerable<RecurringOccurrence>> GetRecentOccurrencesAsync(
        int patternId,
        Guid userId,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        return await _context.RecurringOccurrences
            .Include(o => o.Transaction)
            .Where(o => o.PatternId == patternId && o.Pattern.UserId == userId && !o.IsDeleted)
            .OrderByDescending(o => o.ExpectedDate)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Creates a new occurrence record
    /// </summary>
    public async Task<RecurringOccurrence> CreateOccurrenceAsync(
        RecurringOccurrence occurrence,
        CancellationToken cancellationToken = default)
    {
        var entry = await _context.RecurringOccurrences.AddAsync(occurrence, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entry.Entity;
    }

    /// <summary>
    /// Updates an occurrence record
    /// </summary>
    public async Task<RecurringOccurrence> UpdateOccurrenceAsync(
        RecurringOccurrence occurrence,
        CancellationToken cancellationToken = default)
    {
        occurrence.UpdatedAt = DateTime.UtcNow;
        _context.RecurringOccurrences.Update(occurrence);
        await _context.SaveChangesAsync(cancellationToken);
        return occurrence;
    }

    /// <summary>
    /// Checks if a transaction is already linked to an occurrence
    /// </summary>
    public async Task<bool> IsTransactionLinkedAsync(
        int transactionId,
        CancellationToken cancellationToken = default)
    {
        return await _context.RecurringOccurrences
            .AnyAsync(o => o.TransactionId == transactionId && !o.IsDeleted, cancellationToken);
    }

    // Aggregation operations

    /// <summary>
    /// Gets the total monthly cost of all active patterns for a user
    /// </summary>
    public async Task<decimal> GetTotalMonthlyCostAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var activePatterns = await _context.RecurringPatterns
            .Where(p => p.UserId == userId
                        && (p.Status == RecurringPatternStatus.Active || p.Status == RecurringPatternStatus.AtRisk)
                        && !p.IsDeleted)
            .ToListAsync(cancellationToken);

        return activePatterns.Sum(p => p.GetMonthlyCost());
    }

    /// <summary>
    /// Gets the total annual cost of all active patterns for a user
    /// </summary>
    public async Task<decimal> GetTotalAnnualCostAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var activePatterns = await _context.RecurringPatterns
            .Where(p => p.UserId == userId
                        && (p.Status == RecurringPatternStatus.Active || p.Status == RecurringPatternStatus.AtRisk)
                        && !p.IsDeleted)
            .ToListAsync(cancellationToken);

        return activePatterns.Sum(p => p.GetAnnualCost());
    }

    /// <summary>
    /// Gets pattern statistics for a user
    /// </summary>
    public async Task<(int TotalPatterns, int ActivePatterns, int AtRiskPatterns, decimal TotalMonthlyCost)>
        GetStatisticsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var patterns = await _context.RecurringPatterns
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .ToListAsync(cancellationToken);

        var totalPatterns = patterns.Count;
        var activePatterns = patterns.Count(p => p.Status == RecurringPatternStatus.Active);
        var atRiskPatterns = patterns.Count(p => p.Status == RecurringPatternStatus.AtRisk);
        var totalMonthlyCost = patterns
            .Where(p => p.Status == RecurringPatternStatus.Active || p.Status == RecurringPatternStatus.AtRisk)
            .Sum(p => p.GetMonthlyCost());

        return (totalPatterns, activePatterns, atRiskPatterns, totalMonthlyCost);
    }

    /// <summary>
    /// Gets patterns by category for budget integration
    /// </summary>
    public async Task<IEnumerable<RecurringPattern>> GetByCategoryAsync(
        int categoryId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.RecurringPatterns
            .Include(p => p.Category)
            .Where(p => p.UserId == userId
                        && p.CategoryId == categoryId
                        && !p.IsDeleted)
            .OrderBy(p => p.MerchantName)
            .ToListAsync(cancellationToken);
    }

    // Bulk operations

    /// <summary>
    /// Gets all users who have transactions (for background job processing)
    /// </summary>
    public async Task<IEnumerable<Guid>> GetUserIdsWithTransactionsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Transactions
            .Where(t => !t.IsDeleted)
            .Select(t => t.Account.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Bulk updates pattern statuses after processing
    /// </summary>
    public async Task BulkUpdateStatusAsync(
        IEnumerable<(int PatternId, RecurringPatternStatus NewStatus, int ConsecutiveMisses)> updates,
        CancellationToken cancellationToken = default)
    {
        foreach (var (patternId, newStatus, consecutiveMisses) in updates)
        {
            var pattern = await _context.RecurringPatterns
                .FirstOrDefaultAsync(p => p.Id == patternId && !p.IsDeleted, cancellationToken);

            if (pattern != null)
            {
                pattern.Status = newStatus;
                pattern.ConsecutiveMisses = consecutiveMisses;
                pattern.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
