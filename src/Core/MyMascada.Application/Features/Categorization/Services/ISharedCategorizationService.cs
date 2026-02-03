using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Categorization.Services;

/// <summary>
/// Shared service that encapsulates LLM categorization logic for use by both
/// the categorization pipeline and the standalone AI categorization endpoints
/// </summary>
public interface ISharedCategorizationService
{
    /// <summary>
    /// Gets categorization suggestions for transactions using LLM
    /// Returns multiple suggestions per transaction to support candidate system
    /// </summary>
    Task<LlmCategorizationResponse> GetCategorizationSuggestionsAsync(
        IEnumerable<Transaction> transactions,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the LLM service is available
    /// </summary>
    Task<bool> IsServiceAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the confidence threshold above which suggestions might be auto-applied
    /// (Currently only Rules use auto-apply, but this provides consistency)
    /// </summary>
    decimal GetAutoApplyThreshold();

    /// <summary>
    /// Converts LLM categorization response to categorization candidates
    /// </summary>
    IEnumerable<CategorizationCandidate> ConvertToCategorizationCandidates(
        LlmCategorizationResponse response,
        string appliedBy);
}
