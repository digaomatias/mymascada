using MyMascada.Application.Features.Rules.DTOs;

namespace MyMascada.Application.Features.Rules.Services;

public interface IRuleSuggestionsService
{
    /// <summary>
    /// Analyzes user transactions and generates rule suggestions
    /// </summary>
    Task<RuleSuggestionsResponse> GenerateSuggestionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes specific transactions and suggests rule patterns
    /// </summary>
    Task<List<RuleSuggestionDto>> AnalyzeTransactionsAsync(Guid userId, List<int> transactionIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets suggestions for frequently occurring uncategorized transactions
    /// </summary>
    Task<List<RuleSuggestionDto>> GetUncategorizedSuggestionsAsync(Guid userId, int maxSuggestions = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets suggestions based on similar transactions that were manually categorized
    /// </summary>
    Task<List<RuleSuggestionDto>> GetPatternBasedSuggestionsAsync(Guid userId, int maxSuggestions = 10, CancellationToken cancellationToken = default);
}