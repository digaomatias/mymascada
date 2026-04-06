using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Features.Categorization.Models;
using MyMascada.Application.Features.Categorization.Services;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Categorization.Handlers;

/// <summary>
/// ML handler in the categorization pipeline — similarity matching engine.
/// Categorizes transactions by matching normalized descriptions against the user's
/// own categorization history. Two-tier: exact match then token-overlap fuzzy matching.
/// </summary>
public class MLHandler : CategorizationHandler
{
    private readonly ISimilarityMatchingService _similarityService;
    private readonly CategorizationOptions _options;

    public MLHandler(
        ISimilarityMatchingService similarityService,
        IOptions<CategorizationOptions> options,
        ILogger<MLHandler> logger) : base(logger)
    {
        _similarityService = similarityService;
        _options = options.Value;
    }

    public override string HandlerType => "ML";

    protected override async Task<CategorizationResult> ProcessTransactionsAsync(
        IEnumerable<Transaction> transactions,
        CancellationToken cancellationToken)
    {
        var result = new CategorizationResult();
        var transactionsList = transactions.ToList();

        if (!transactionsList.Any())
        {
            UpdateMetrics(result, 0);
            return result;
        }

        var firstTransaction = transactionsList.First();
        var userId = firstTransaction.Account?.UserId;
        if (userId == null)
        {
            _logger.LogWarning("MLHandler: Cannot process — no user ID found in transactions");
            UpdateMetrics(result, 0);
            return result;
        }

        var autoApplyThreshold = _options.MLAutoApplyThreshold;
        var processedCount = 0;

        foreach (var transaction in transactionsList)
        {
            var description = transaction.Description;
            var normalized = DescriptionNormalizer.Normalize(description);

            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            var match = await _similarityService.FindBestMatchAsync(userId.Value, normalized, cancellationToken);
            if (match == null)
                continue;

            if (match.Confidence >= autoApplyThreshold)
            {
                // High confidence — auto-apply
                var categorized = CreateCategorizedTransaction(
                    transaction,
                    match.CategoryId,
                    match.CategoryName,
                    match.Confidence,
                    $"{match.MatchType} match on '{match.MatchedDescription}'");

                result.AutoAppliedTransactions.Add(categorized);
                result.CategorizedTransactions.Add(categorized);
                processedCount++;

                _logger.LogDebug(
                    "MLHandler: Auto-applying category {CategoryId} ({CategoryName}) to transaction {TransactionId} " +
                    "with confidence {Confidence:F2} ({MatchType} match)",
                    match.CategoryId, match.CategoryName, transaction.Id,
                    match.Confidence, match.MatchType);
            }
            else
            {
                // Medium confidence — create candidate for user review
                var candidate = new CategorizationCandidate
                {
                    TransactionId = transaction.Id,
                    CategoryId = match.CategoryId,
                    CategorizationMethod = CandidateMethod.ML,
                    ConfidenceScore = match.Confidence,
                    ProcessedBy = "MLHandler",
                    Reasoning = $"{match.MatchType} match: '{match.MatchedDescription}' (confidence: {match.Confidence:F2})",
                    Status = CandidateStatus.Pending,
                    CreatedBy = $"MLHandler-{userId.Value}",
                    UpdatedBy = $"MLHandler-{userId.Value}"
                };

                result.Candidates.Add(candidate);
                result.CategorizedTransactions.Add(CreateCategorizedTransaction(
                    transaction,
                    match.CategoryId,
                    match.CategoryName,
                    match.Confidence,
                    $"{match.MatchType} match candidate"));

                processedCount++;

                _logger.LogDebug(
                    "MLHandler: Creating candidate for transaction {TransactionId} — " +
                    "category {CategoryId} ({CategoryName}), confidence {Confidence:F2}",
                    transaction.Id, match.CategoryId, match.CategoryName, match.Confidence);
            }
        }

        UpdateMetrics(result, processedCount);

        _logger.LogInformation(
            "MLHandler: Processed {ProcessedCount}/{TotalCount} transactions " +
            "({AutoApplied} auto-applied, {Candidates} candidates)",
            processedCount, transactionsList.Count,
            result.AutoAppliedTransactions.Count, result.Candidates.Count);

        return result;
    }
}
