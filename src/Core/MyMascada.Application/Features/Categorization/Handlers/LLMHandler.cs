using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Models;
using MyMascada.Application.Features.Categorization.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Categorization.Handlers;

/// <summary>
/// Final handler in the chain - applies LLM categorization for complex transactions.
/// Gated by subscription tier: Free users are skipped, Pro users check quota, SelfHosted unlimited.
/// </summary>
public class LLMHandler : CategorizationHandler
{
    private readonly ISharedCategorizationService _sharedCategorizationService;
    private readonly ISubscriptionService _subscriptionService;

    public LLMHandler(
        ISharedCategorizationService sharedCategorizationService,
        ISubscriptionService subscriptionService,
        ILogger<LLMHandler> logger) : base(logger)
    {
        _sharedCategorizationService = sharedCategorizationService;
        _subscriptionService = subscriptionService;
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

        // Resolve userId from the Account navigation property on the first transaction.
        // Account is a required relationship (non-nullable FK) so it's always loaded when
        // the pipeline includes Account in the query. Fall back gracefully if not loaded.
        var firstTransaction = transactionsList.First();
        var userId = firstTransaction.Account?.UserId;
        if (userId == null)
        {
            _logger.LogWarning("Cannot process LLM categorization - Account not loaded on transaction {TransactionId}", firstTransaction.Id);
            return result;
        }

        // Single call: checks tier, quota, and returns remaining count
        var accessResult = await _subscriptionService.CanUseLlmCategorizationAsync(userId.Value, cancellationToken);

        if (!accessResult.IsAllowed)
        {
            if (accessResult.Tier == SubscriptionTier.Free)
            {
                _logger.LogInformation(
                    "LLM Handler skipped for free-tier user {UserId} — {Count} transactions left for remaining handlers",
                    userId.Value, transactionsList.Count);
            }
            else
            {
                _logger.LogInformation(
                    "LLM Handler skipped for user {UserId} — monthly LLM quota exhausted",
                    userId.Value);
                result.Errors.Add("Monthly LLM categorization quota exceeded. Transactions will be categorized by rules and ML matching.");
            }
            return result;
        }

        // Determine the batch to send to the LLM (may be capped by quota)
        var llmBatch = transactionsList;
        var remaining = accessResult.RemainingQuota;

        // Cap the batch to remaining quota without mutating the original list
        if (remaining < int.MaxValue && transactionsList.Count > remaining)
        {
            _logger.LogInformation(
                "LLM Handler capping batch from {Requested} to {Remaining} transactions (quota limit) for user {UserId}",
                transactionsList.Count, remaining, userId.Value);
            llmBatch = transactionsList.Take(remaining).ToList();
        }

        _logger.LogInformation("LLM Handler processing {TransactionCount} transactions - this will incur AI costs",
            llmBatch.Count);

        try
        {
            // Use shared categorization service to get LLM suggestions
            var llmResponse = await _sharedCategorizationService.GetCategorizationSuggestionsAsync(
                llmBatch, userId.Value, cancellationToken);

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

            // Keep original transactionsList intact for downstream state
            result.RemainingTransactions = transactionsList;
            result.Metrics.ProcessedByLLM = candidates.Count();
            result.Metrics.EstimatedCostSavings = CalculateCostSavings(candidates.Count());

            // Record usage based on how many transactions were sent to the LLM
            if (llmBatch.Count > 0)
            {
                await _subscriptionService.RecordLlmUsageAsync(userId.Value, llmBatch.Count, cancellationToken);
            }

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
        return candidateCount * 0.01m;
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
