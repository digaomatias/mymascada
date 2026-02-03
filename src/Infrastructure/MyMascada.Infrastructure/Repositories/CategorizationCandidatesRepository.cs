using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for managing categorization candidates
/// </summary>
public class CategorizationCandidatesRepository : ICategorizationCandidatesRepository
{
    private readonly ApplicationDbContext _context;

    public CategorizationCandidatesRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<CategorizationCandidate>> GetPendingCandidatesForTransactionAsync(
        int transactionId, CancellationToken cancellationToken = default)
    {
        return await _context.CategorizationCandidates
            .Include(c => c.Category)
            .Where(c => c.TransactionId == transactionId && c.Status == CandidateStatus.Pending)
            .OrderByDescending(c => c.ConfidenceScore)
            .ThenBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<CategorizationCandidate>> GetPendingCandidatesForTransactionsAsync(
        IEnumerable<int> transactionIds, CancellationToken cancellationToken = default)
    {
        var ids = transactionIds.ToList();
        return await _context.CategorizationCandidates
            .Include(c => c.Category)
            .Where(c => ids.Contains(c.TransactionId) && c.Status == CandidateStatus.Pending)
            .OrderBy(c => c.TransactionId)
            .ThenByDescending(c => c.ConfidenceScore)
            .ThenBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<CategorizationCandidate>> GetCandidatesForTransactionsByMethodAsync(
        IEnumerable<int> transactionIds, string categorizationMethod, CancellationToken cancellationToken = default)
    {
        var ids = transactionIds.ToList();
        if (!ids.Any())
            return new List<CategorizationCandidate>();

        return await _context.CategorizationCandidates
            .Include(c => c.Category)
            .Include(c => c.Transaction)
            .ThenInclude(t => t.Account)
            .Where(c => ids.Contains(c.TransactionId) && c.CategorizationMethod == categorizationMethod)
            .OrderBy(c => c.TransactionId)
            .ThenByDescending(c => c.ConfidenceScore)
            .ThenBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<CategorizationCandidate>> GetPendingCandidatesForUserAsync(
        Guid userId, int limit = 500, CancellationToken cancellationToken = default)
    {
        return await _context.CategorizationCandidates
            .Include(c => c.Category)
            .Include(c => c.Transaction)
            .ThenInclude(t => t.Account)
            .Where(c => c.Transaction.Account.UserId == userId && c.Status == CandidateStatus.Pending)
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<CategorizationCandidate>> AddCandidatesBatchAsync(
        IEnumerable<CategorizationCandidate> candidates, CancellationToken cancellationToken = default)
    {
        var candidatesList = candidates.ToList();
        
        if (!candidatesList.Any())
            return candidatesList;

        await _context.CategorizationCandidates.AddRangeAsync(candidatesList, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        
        return candidatesList;
    }

    public async Task<CategorizationCandidate?> GetByIdAsync(
        int candidateId, CancellationToken cancellationToken = default)
    {
        return await _context.CategorizationCandidates
            .Include(c => c.Category)
            .Include(c => c.Transaction)
            .ThenInclude(t => t.Account)
            .FirstOrDefaultAsync(c => c.Id == candidateId, cancellationToken);
    }

    public async Task UpdateCandidateAsync(
        CategorizationCandidate candidate, CancellationToken cancellationToken = default)
    {
        _context.CategorizationCandidates.Update(candidate);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkCandidatesAsAppliedBatchAsync(
        IEnumerable<int> candidateIds, string appliedBy, CancellationToken cancellationToken = default)
    {
        var ids = candidateIds.ToList();
        if (!ids.Any()) return;

        var candidates = await _context.CategorizationCandidates
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            candidate.MarkAsApplied(appliedBy);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BulkMarkCandidatesAsAppliedAsync(
        IEnumerable<int> candidateIds, string appliedBy, CancellationToken cancellationToken = default)
    {
        var ids = candidateIds.ToList();
        if (!ids.Any()) return;

        // Truncate appliedBy to fit the database constraint (50 characters max)
        var truncatedAppliedBy = appliedBy?.Length > 50 ? appliedBy.Substring(0, 50) : appliedBy;

        // Use ExecuteUpdate for bulk operation without loading entities into change tracker
        await _context.CategorizationCandidates
            .Where(c => ids.Contains(c.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.Status, CandidateStatus.Applied)
                .SetProperty(c => c.AppliedBy, truncatedAppliedBy)
                .SetProperty(c => c.AppliedAt, DateTime.UtcNow),
                cancellationToken);
    }

    public async Task MarkCandidatesAsRejectedBatchAsync(
        IEnumerable<int> candidateIds, string rejectedBy, CancellationToken cancellationToken = default)
    {
        var ids = candidateIds.ToList();
        if (!ids.Any()) return;

        var candidates = await _context.CategorizationCandidates
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            candidate.MarkAsRejected(rejectedBy);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Dictionary<int, List<CategorizationCandidate>>> GetCandidatesGroupedByTransactionAsync(
        IEnumerable<int> transactionIds, CancellationToken cancellationToken = default)
    {
        var ids = transactionIds.ToList();
        var candidates = await _context.CategorizationCandidates
            .Include(c => c.Category)
            .Where(c => ids.Contains(c.TransactionId) && c.Status == CandidateStatus.Pending)
            .OrderByDescending(c => c.ConfidenceScore)
            .ToListAsync(cancellationToken);

        return candidates
            .GroupBy(c => c.TransactionId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public async Task CleanupOldCandidatesAsync(
        DateTime olderThan, CancellationToken cancellationToken = default)
    {
        // Remove old applied/rejected candidates (keep pending ones)
        var oldCandidates = await _context.CategorizationCandidates
            .Where(c => c.CreatedAt < olderThan && 
                       (c.Status == CandidateStatus.Applied || c.Status == CandidateStatus.Rejected))
            .ToListAsync(cancellationToken);

        if (oldCandidates.Any())
        {
            _context.CategorizationCandidates.RemoveRange(oldCandidates);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<CategorizationCandidateStats> GetCandidateStatsAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var candidates = await _context.CategorizationCandidates
            .Include(c => c.Transaction)
            .ThenInclude(t => t.Account)
            .Where(c => c.Transaction.Account.UserId == userId)
            .ToListAsync(cancellationToken);

        if (!candidates.Any())
        {
            return new CategorizationCandidateStats();
        }

        return new CategorizationCandidateStats
        {
            TotalPending = candidates.Count(c => c.Status == CandidateStatus.Pending),
            TotalApplied = candidates.Count(c => c.Status == CandidateStatus.Applied),
            TotalRejected = candidates.Count(c => c.Status == CandidateStatus.Rejected),
            AverageConfidence = candidates.Average(c => c.ConfidenceScore),
            ByMethod = candidates
                .GroupBy(c => c.CategorizationMethod)
                .ToDictionary(g => g.Key, g => g.Count()),
            ByStatus = candidates
                .GroupBy(c => c.Status)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    public async Task<HashSet<int>> GetTransactionIdsWithPendingCandidatesAsync(
        IEnumerable<int> transactionIds, CancellationToken cancellationToken = default)
    {
        var ids = transactionIds.ToList();
        if (!ids.Any())
            return new HashSet<int>();

        var idsWithCandidates = await _context.CategorizationCandidates
            .Where(c => ids.Contains(c.TransactionId) && c.Status == CandidateStatus.Pending)
            .Select(c => c.TransactionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new HashSet<int>(idsWithCandidates);
    }
}