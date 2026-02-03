using Microsoft.EntityFrameworkCore;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Repositories;

/// <summary>
/// Repository for managing rule suggestions
/// </summary>
public class RuleSuggestionRepository : IRuleSuggestionRepository
{
    private readonly ApplicationDbContext _context;

    public RuleSuggestionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets all pending rule suggestions for a user
    /// </summary>
    public async Task<IEnumerable<RuleSuggestion>> GetPendingSuggestionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.RuleSuggestions
            .Include(rs => rs.SuggestedCategory)
            .Include(rs => rs.SampleTransactions)
            .Where(rs => rs.UserId == userId && !rs.IsDeleted && !rs.IsAccepted && !rs.IsRejected)
            .OrderByDescending(rs => rs.ConfidenceScore)
            .ThenByDescending(rs => rs.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets all rule suggestions for a user (including processed ones)
    /// </summary>
    public async Task<IEnumerable<RuleSuggestion>> GetAllSuggestionsAsync(Guid userId, bool includeProcessed = false, CancellationToken cancellationToken = default)
    {
        var query = _context.RuleSuggestions
            .Include(rs => rs.SuggestedCategory)
            .Include(rs => rs.SampleTransactions)
            .Where(rs => rs.UserId == userId && !rs.IsDeleted);

        if (!includeProcessed)
        {
            query = query.Where(rs => !rs.IsAccepted && !rs.IsRejected);
        }

        return await query
            .OrderByDescending(rs => rs.ConfidenceScore)
            .ThenByDescending(rs => rs.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets a specific rule suggestion by ID for a user
    /// </summary>
    public async Task<RuleSuggestion?> GetSuggestionByIdAsync(int suggestionId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.RuleSuggestions
            .Include(rs => rs.SuggestedCategory)
            .Include(rs => rs.SampleTransactions)
            .Include(rs => rs.CreatedRule)
            .FirstOrDefaultAsync(rs => rs.Id == suggestionId && rs.UserId == userId && !rs.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Creates a new rule suggestion
    /// </summary>
    public async Task<RuleSuggestion> CreateSuggestionAsync(RuleSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        var entry = await _context.RuleSuggestions.AddAsync(suggestion, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        
        // Load navigation properties
        await _context.Entry(entry.Entity)
            .Reference(rs => rs.SuggestedCategory)
            .LoadAsync(cancellationToken);
        
        await _context.Entry(entry.Entity)
            .Collection(rs => rs.SampleTransactions)
            .LoadAsync(cancellationToken);

        return entry.Entity;
    }

    /// <summary>
    /// Updates an existing rule suggestion
    /// </summary>
    public async Task<RuleSuggestion> UpdateSuggestionAsync(RuleSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        suggestion.UpdatedAt = DateTime.UtcNow;
        _context.RuleSuggestions.Update(suggestion);
        await _context.SaveChangesAsync(cancellationToken);
        return suggestion;
    }

    /// <summary>
    /// Deletes old rule suggestions (cleanup)
    /// </summary>
    public async Task DeleteOldSuggestionsAsync(Guid userId, DateTime olderThan, CancellationToken cancellationToken = default)
    {
        var oldSuggestions = await _context.RuleSuggestions
            .Where(rs => rs.UserId == userId && rs.CreatedAt < olderThan && !rs.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var suggestion in oldSuggestions)
        {
            suggestion.IsDeleted = true;
            suggestion.DeletedAt = DateTime.UtcNow;
        }

        if (oldSuggestions.Any())
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Checks if similar suggestions already exist for a user
    /// </summary>
    public async Task<IEnumerable<RuleSuggestion>> GetSimilarSuggestionsAsync(Guid userId, string pattern, int categoryId, CancellationToken cancellationToken = default)
    {
        return await _context.RuleSuggestions
            .Where(rs => rs.UserId == userId && 
                        !rs.IsDeleted && 
                        rs.Pattern.ToLower() == pattern.ToLower() && 
                        rs.SuggestedCategoryId == categoryId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets summary statistics for rule suggestions
    /// </summary>
    public async Task<(int TotalSuggestions, double AverageConfidence, DateTime? LastGenerated)> GetSuggestionStatisticsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var pendingSuggestions = await _context.RuleSuggestions
            .Where(rs => rs.UserId == userId && !rs.IsDeleted && !rs.IsAccepted && !rs.IsRejected)
            .ToListAsync(cancellationToken);

        if (!pendingSuggestions.Any())
        {
            return (0, 0.0, null);
        }

        var totalSuggestions = pendingSuggestions.Count;
        var averageConfidence = pendingSuggestions.Average(rs => rs.ConfidenceScore);
        var lastGenerated = pendingSuggestions.Max(rs => rs.CreatedAt);

        return (totalSuggestions, averageConfidence, lastGenerated);
    }
}