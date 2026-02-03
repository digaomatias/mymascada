using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

/// <summary>
/// Repository interface for managing rule suggestions
/// </summary>
public interface IRuleSuggestionRepository
{
    /// <summary>
    /// Gets all pending rule suggestions for a user
    /// </summary>
    Task<IEnumerable<RuleSuggestion>> GetPendingSuggestionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all rule suggestions for a user (including processed ones)
    /// </summary>
    Task<IEnumerable<RuleSuggestion>> GetAllSuggestionsAsync(Guid userId, bool includeProcessed = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific rule suggestion by ID for a user
    /// </summary>
    Task<RuleSuggestion?> GetSuggestionByIdAsync(int suggestionId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new rule suggestion
    /// </summary>
    Task<RuleSuggestion> CreateSuggestionAsync(RuleSuggestion suggestion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing rule suggestion
    /// </summary>
    Task<RuleSuggestion> UpdateSuggestionAsync(RuleSuggestion suggestion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes old rule suggestions (cleanup)
    /// </summary>
    Task DeleteOldSuggestionsAsync(Guid userId, DateTime olderThan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if similar suggestions already exist for a user
    /// </summary>
    Task<IEnumerable<RuleSuggestion>> GetSimilarSuggestionsAsync(Guid userId, string pattern, int categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets summary statistics for rule suggestions
    /// </summary>
    Task<(int TotalSuggestions, double AverageConfidence, DateTime? LastGenerated)> GetSuggestionStatisticsAsync(Guid userId, CancellationToken cancellationToken = default);
}