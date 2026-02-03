using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Models;
using MyMascada.Application.Features.Categorization.Services;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Categorization.Handlers;

/// <summary>
/// Final handler in the chain - applies LLM categorization for complex transactions
/// Slow processing with high cost - used only for novel/complex cases
/// Uses existing LLM service
/// </summary>
public class LLMHandler : CategorizationHandler
{
    private readonly ISharedCategorizationService _sharedCategorizationService;

    public LLMHandler(
        ISharedCategorizationService sharedCategorizationService,
        ILogger<LLMHandler> logger) : base(logger)
    {
        _sharedCategorizationService = sharedCategorizationService;
    }

    public override string HandlerType => "LLM";

    protected override async Task<CategorizationResult> ProcessTransactionsAsync(
        IEnumerable<Transaction> transactions, 
        CancellationToken cancellationToken)
    {
        var result = new CategorizationResult();
        var transactionsList = transactions.ToList();

        if (!transactionsList.Any())
            return result;

        _logger.LogInformation("LLM Handler processing {TransactionCount} transactions - this will incur AI costs",
            transactionsList.Count);

        try
        {
            // Get user ID from first transaction
            var userId = transactionsList.First().Account?.UserId;
            if (userId == null)
            {
                _logger.LogWarning("Cannot process LLM categorization - no user ID found");
                return result;
            }

            // Use shared categorization service to get LLM suggestions
            var llmResponse = await _sharedCategorizationService.GetCategorizationSuggestionsAsync(
                transactionsList, userId.Value, cancellationToken);

            if (!llmResponse.Success)
            {
                _logger.LogError("LLM service failed: {Errors}", string.Join(", ", llmResponse.Errors));
                result.Errors.AddRange(llmResponse.Errors);
                return result;
            }

            // Convert LLM response to categorization candidates
            var candidates = _sharedCategorizationService.ConvertToCategorizationCandidates(
                llmResponse, $"LLMHandler-{userId}");

            // Add all LLM suggestions to candidates list (no auto-apply for LLM)
            result.Candidates.AddRange(candidates);
            
            // For LLM handler, we don't auto-apply any categorizations
            // All suggestions go to the candidates system for user review
            result.RemainingTransactions = transactionsList;
            result.Metrics.ProcessedByLLM = candidates.Count();
            result.Metrics.EstimatedCostSavings = CalculateCostSavings(candidates.Count());

            // Update category distribution metrics for reporting
            foreach (var candidate in candidates.GroupBy(c => c.Category?.Name ?? "Unknown"))
            {
                result.Metrics.CategoryDistribution[candidate.Key] = candidate.Count();
                
                var avgConfidence = candidate.Average(c => c.ConfidenceScore);
                var confidenceRange = GetConfidenceRange(avgConfidence);
                result.Metrics.ConfidenceDistribution[confidenceRange] = 
                    result.Metrics.ConfidenceDistribution.GetValueOrDefault(confidenceRange, 0) + candidate.Count();
            }

            _logger.LogInformation("LLM Handler created {CandidateCount} candidates for user approval", 
                candidates.Count());

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LLM categorization processing");
            result.Errors.Add($"LLM processing failed: {ex.Message}");
            return result;
        }
    }

    private static decimal CalculateCostSavings(int candidateCount)
    {
        // Estimate cost savings by avoiding immediate LLM processing for every transaction
        // This is rough - actual savings would be calculated based on real LLM costs
        return candidateCount * 0.01m; // $0.01 per transaction
    }

    private static string GetConfidenceRange(decimal confidence)
    {
        return confidence switch
        {
            >= 0.9m => "High (90-100%)",
            >= 0.7m => "Medium (70-89%)",
            >= 0.5m => "Low (50-69%)",
            _ => "Very Low (<50%)"
        };
    }
}
