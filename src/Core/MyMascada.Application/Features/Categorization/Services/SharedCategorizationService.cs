using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using System.Text.Json;

namespace MyMascada.Application.Features.Categorization.Services;

/// <summary>
/// Shared service that encapsulates LLM categorization logic for use by both
/// the categorization pipeline and the standalone AI categorization endpoints
/// </summary>
public class SharedCategorizationService : ISharedCategorizationService
{
    private readonly ILlmCategorizationService _llmService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<SharedCategorizationService> _logger;

    public SharedCategorizationService(
        ILlmCategorizationService llmService,
        ITransactionRepository transactionRepository,
        ILogger<SharedCategorizationService> logger)
    {
        _llmService = llmService;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    public async Task<LlmCategorizationResponse> GetCategorizationSuggestionsAsync(
        IEnumerable<Transaction> transactions,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting categorization suggestions for {TransactionCount} transactions for user {UserId}",
                transactions.Count(), userId);

            // Check if LLM service is available
            if (!await IsServiceAvailableAsync(cancellationToken))
            {
                return new LlmCategorizationResponse
                {
                    Success = false,
                    Errors = new List<string> { "LLM categorization service is currently unavailable" }
                };
            }

            // Get categories for context (rules are now handled by the categorization pipeline)
            var categories = await _transactionRepository.GetCategoriesAsync(userId, cancellationToken);

            // Use existing LLM service to get suggestions
            var response = await _llmService.CategorizeTransactionsAsync(
                transactions, categories, cancellationToken);

            _logger.LogDebug("LLM service returned {Success} with {CategorizedCount} categorizations and {ErrorCount} errors",
                response.Success, response.Categorizations.Count, response.Errors.Count);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categorization suggestions for user {UserId}", userId);
            return new LlmCategorizationResponse
            {
                Success = false,
                Errors = new List<string> { $"Failed to get categorization suggestions: {ex.Message}" }
            };
        }
    }

    public async Task<bool> IsServiceAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _llmService.IsServiceAvailableAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking LLM service availability");
            return false;
        }
    }

    public decimal GetAutoApplyThreshold()
    {
        // For LLM suggestions, we typically don't auto-apply
        // They should go through the candidate system for user approval
        // This threshold is mainly used for Rules Handler
        return 0.95m;
    }

    public IEnumerable<CategorizationCandidate> ConvertToCategorizationCandidates(
        LlmCategorizationResponse response,
        string appliedBy)
    {
        var candidates = new List<CategorizationCandidate>();

        if (!response.Success || !response.Categorizations.Any())
        {
            return candidates;
        }

        foreach (var categorization in response.Categorizations)
        {
            foreach (var suggestion in categorization.Suggestions)
            {
                var candidate = new CategorizationCandidate
                {
                    TransactionId = categorization.TransactionId,
                    CategoryId = suggestion.CategoryId,
                    CategorizationMethod = CandidateMethod.LLM,
                    ConfidenceScore = suggestion.Confidence,
                    ProcessedBy = "LLMHandler",
                    Reasoning = suggestion.Reasoning,
                    Metadata = SerializeMetadata(new
                    {
                        MatchingRules = suggestion.MatchingRules,
                        RequiresReview = categorization.RequiresReview,
                        SuggestedRule = categorization.SuggestedRule,
                        IsRecommended = suggestion.CategoryId == categorization.RecommendedCategoryId
                    }),
                    Status = CandidateStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = appliedBy,
                    UpdatedBy = appliedBy
                };

                candidates.Add(candidate);
            }
        }

        _logger.LogDebug("Converted LLM response to {CandidateCount} categorization candidates", candidates.Count);
        return candidates;
    }

    private static string SerializeMetadata(object metadata)
    {
        try
        {
            return JsonSerializer.Serialize(metadata, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });
        }
        catch (Exception)
        {
            return "{}";
        }
    }
}
