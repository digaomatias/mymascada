using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Models;
using MyMascada.Domain.Entities;
using System.Text.Json;

namespace MyMascada.Application.Features.Categorization.Handlers;

/// <summary>
/// Second handler in the chain - applies bank category mappings from external providers (e.g., Akahu).
/// Runs after Rules handler but before ML/LLM handlers.
/// Fast processing with minimal cost (uses pre-resolved mappings).
/// </summary>
public class BankCategoryHandler : CategorizationHandler
{
    private readonly IBankCategoryMappingService _bankCategoryMappingService;
    private readonly IBankConnectionRepository _bankConnectionRepository;
    private readonly CategorizationOptions _options;

    public BankCategoryHandler(
        IBankCategoryMappingService bankCategoryMappingService,
        IBankConnectionRepository bankConnectionRepository,
        IOptions<CategorizationOptions> options,
        ILogger<BankCategoryHandler> logger) : base(logger)
    {
        _bankCategoryMappingService = bankCategoryMappingService;
        _bankConnectionRepository = bankConnectionRepository;
        _options = options.Value;
    }

    public override string HandlerType => "BankCategory";

    protected override async Task<CategorizationResult> ProcessTransactionsAsync(
        IEnumerable<Transaction> transactions,
        CancellationToken cancellationToken)
    {
        var result = new CategorizationResult();
        var transactionsList = transactions.ToList();

        if (!transactionsList.Any())
            return result;

        // Get transactions that have bank categories
        var transactionsWithBankCategory = transactionsList
            .Where(t => !string.IsNullOrEmpty(t.BankCategory))
            .ToList();

        if (!transactionsWithBankCategory.Any())
        {
            _logger.LogInformation("BankCategoryHandler: No transactions have bank categories, passing all to next handler");
            return result;
        }

        // Get user ID from first transaction
        var firstTransaction = transactionsWithBankCategory.First();
        var userId = firstTransaction.Account?.UserId;
        if (userId == null)
        {
            _logger.LogWarning("BankCategoryHandler: Cannot process - no user ID found in transactions");
            return result;
        }

        _logger.LogInformation(
            "BankCategoryHandler: Processing {TransactionCount} transactions with bank categories for user {UserId}",
            transactionsWithBankCategory.Count, userId);

        // Get the provider ID (default to "akahu" if not found)
        var providerId = await GetProviderIdAsync(firstTransaction.AccountId, cancellationToken) ?? "akahu";

        // Collect unique bank categories
        var uniqueBankCategories = transactionsWithBankCategory
            .Select(t => t.BankCategory!)
            .Distinct()
            .ToList();

        _logger.LogInformation(
            "BankCategoryHandler: Resolving {CategoryCount} unique bank categories: [{Categories}]",
            uniqueBankCategories.Count, string.Join(", ", uniqueBankCategories));

        // Resolve bank category mappings
        Dictionary<string, BankCategoryMappingResult> categoryMappings;
        try
        {
            categoryMappings = await _bankCategoryMappingService.ResolveAndCreateMappingsAsync(
                uniqueBankCategories,
                providerId,
                userId.Value,
                cancellationToken);

            _logger.LogInformation("BankCategoryHandler: Resolved {Count} bank category mappings", categoryMappings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BankCategoryHandler: Failed to resolve bank category mappings, passing to next handler");
            return result;
        }

        // Process each transaction with a bank category
        foreach (var transaction in transactionsWithBankCategory)
        {
            try
            {
                if (!categoryMappings.TryGetValue(transaction.BankCategory!, out var mapping))
                {
                    _logger.LogDebug(
                        "BankCategoryHandler: No mapping found for bank category '{BankCategory}' on transaction {TransactionId}",
                        transaction.BankCategory, transaction.Id);
                    continue;
                }

                // Skip if mapping is excluded - let next handler try to categorize
                if (mapping.IsExcluded)
                {
                    _logger.LogInformation(
                        "BankCategoryHandler: Bank category '{BankCategory}' is excluded from auto-categorization, " +
                        "skipping transaction {TransactionId} (will be processed by next handler)",
                        transaction.BankCategory, transaction.Id);
                    continue;
                }

                // Skip if mapping has CategoryId of 0 (invalid/unmapped)
                if (mapping.CategoryId <= 0)
                {
                    _logger.LogDebug(
                        "BankCategoryHandler: Mapping for '{BankCategory}' has invalid CategoryId, skipping transaction {TransactionId}",
                        transaction.BankCategory, transaction.Id);
                    continue;
                }

                var canAutoApply = mapping.ConfidenceScore >= _options.AutoApplyConfidenceThreshold;
                var reason = BuildMatchReason(transaction.BankCategory!, mapping);

                var metadata = new Dictionary<string, object>
                {
                    ["BankCategory"] = transaction.BankCategory!,
                    ["ProviderId"] = providerId,
                    ["MappingId"] = mapping.Mapping?.Id ?? 0,
                    ["WasExactMatch"] = mapping.WasExactMatch,
                    ["WasCreatedByAI"] = mapping.WasCreatedByAI,
                    ["MatchedAt"] = DateTime.UtcNow
                };

                if (canAutoApply)
                {
                    // Create CategorizedTransaction for immediate application
                    var categorizedTransaction = CreateCategorizedTransaction(
                        transaction,
                        mapping.CategoryId,
                        mapping.CategoryName,
                        mapping.ConfidenceScore,
                        reason,
                        metadata);

                    result.AutoAppliedTransactions.Add(categorizedTransaction);
                    _logger.LogInformation(
                        "BankCategoryHandler: Bank category '{BankCategory}' matched transaction {TransactionId} with high confidence {Confidence} - will auto-apply to category '{CategoryName}'",
                        transaction.BankCategory, transaction.Id, mapping.ConfidenceScore, mapping.CategoryName);
                }
                else
                {
                    // Create CategorizationCandidate for user review
                    var candidate = new CategorizationCandidate
                    {
                        TransactionId = transaction.Id,
                        CategoryId = mapping.CategoryId,
                        CategorizationMethod = CandidateMethod.BankCategory,
                        ConfidenceScore = mapping.ConfidenceScore,
                        ProcessedBy = "BankCategoryHandler",
                        Reasoning = reason,
                        Metadata = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        }),
                        Status = CandidateStatus.Pending,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedBy = $"BankCategoryHandler-{userId}",
                        UpdatedBy = $"BankCategoryHandler-{userId}"
                    };

                    result.Candidates.Add(candidate);
                    _logger.LogInformation(
                        "BankCategoryHandler: Bank category '{BankCategory}' matched transaction {TransactionId} with confidence {Confidence} - created candidate for review",
                        transaction.BankCategory, transaction.Id, mapping.ConfidenceScore);
                }

                // Record the mapping application for statistics
                if (mapping.Mapping?.Id > 0)
                {
                    try
                    {
                        await _bankCategoryMappingService.RecordMappingApplicationAsync(mapping.Mapping.Id, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "BankCategoryHandler: Failed to record mapping application for mapping {MappingId}", mapping.Mapping.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BankCategoryHandler: Error processing transaction {TransactionId}", transaction.Id);
            }
        }

        // Update result with categorized transactions for metrics tracking
        result.CategorizedTransactions = result.AutoAppliedTransactions.ToList();
        UpdateMetrics(result, result.AutoAppliedTransactions.Count + result.Candidates.Count);

        // Update category distribution metrics
        foreach (var categorized in result.AutoAppliedTransactions)
        {
            result.Metrics.CategoryDistribution[categorized.CategoryName] =
                result.Metrics.CategoryDistribution.GetValueOrDefault(categorized.CategoryName, 0) + 1;

            var confidenceRange = GetConfidenceRange(categorized.ConfidenceScore);
            result.Metrics.ConfidenceDistribution[confidenceRange] =
                result.Metrics.ConfidenceDistribution.GetValueOrDefault(confidenceRange, 0) + 1;
        }

        _logger.LogInformation(
            "BankCategoryHandler completed: {AutoAppliedCount} auto-applied, {CandidateCount} candidates created",
            result.AutoAppliedTransactions.Count, result.Candidates.Count);

        return result;
    }

    private async Task<string?> GetProviderIdAsync(int accountId, CancellationToken cancellationToken)
    {
        try
        {
            var bankConnection = await _bankConnectionRepository.GetByAccountIdAsync(accountId, cancellationToken);
            return bankConnection?.ProviderId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BankCategoryHandler: Failed to get provider ID for account {AccountId}", accountId);
            return null;
        }
    }

    private static string BuildMatchReason(string bankCategory, BankCategoryMappingResult mapping)
    {
        if (mapping.WasExactMatch)
        {
            return $"Bank category '{bankCategory}' exactly matches user category '{mapping.CategoryName}'";
        }

        if (mapping.WasCreatedByAI)
        {
            return $"Bank category '{bankCategory}' mapped to '{mapping.CategoryName}' by AI";
        }

        return $"Bank category '{bankCategory}' mapped to '{mapping.CategoryName}'";
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
