using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Handlers;
using MyMascada.Application.Features.Categorization.Models;
using MyMascada.Application.Features.Transactions.Queries;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using System.Diagnostics;
using System.Text.Json;

namespace MyMascada.Application.Features.Categorization.Services;

/// <summary>
/// Service for applying categorization rules to filtered transactions
/// Designed for manual user-initiated rule application in the categorization UI
/// </summary>
public class RuleAutoCategorizationService : IRuleAutoCategorizationService
{
    private readonly RulesHandler _rulesHandler;
    private readonly ICategorizationCandidatesService _candidatesService;
    private readonly ICategorizationCandidatesRepository _candidatesRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ILogger<RuleAutoCategorizationService> _logger;

    public RuleAutoCategorizationService(
        RulesHandler rulesHandler,
        ICategorizationCandidatesService candidatesService,
        ICategorizationCandidatesRepository candidatesRepository,
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        ILogger<RuleAutoCategorizationService> logger)
    {
        _rulesHandler = rulesHandler;
        _candidatesService = candidatesService;
        _candidatesRepository = candidatesRepository;
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _logger = logger;
    }

    public async Task<RuleAutoCategorizationResult> PreviewRuleApplicationAsync(
        TransactionFilterCriteria filterCriteria,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting rule application preview for user {UserId}", userId);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Get ALL filtered transactions (including those with existing rule candidates)
            var allFilteredTransactions = await GetAllFilteredTransactionsAsync(
                filterCriteria, userId, cancellationToken);

            if (!allFilteredTransactions.Any())
            {
                return new RuleAutoCategorizationResult
                {
                    TotalTransactionsExamined = 0,
                    TransactionsSkipped = 0,
                    TransactionsMatched = 0,
                    TransactionsUnmatched = 0,
                    Summary = "No transactions found matching the current filters.",
                    IsPreview = true,
                    ProcessingTime = stopwatch.Elapsed
                };
            }

            // Get existing rule candidates for these transactions
            var transactionIds = allFilteredTransactions.Select(t => t.Id).ToArray();
            var existingRuleCandidates = await _candidatesRepository
                .GetCandidatesForTransactionsByMethodAsync(transactionIds, "Rule", cancellationToken);

            // Get transactions that don't already have rule candidates for new rule generation
            var transactionsWithoutRuleCandidates = allFilteredTransactions
                .Where(t => !existingRuleCandidates.Any(c => c.TransactionId == t.Id && c.Status == CandidateStatus.Pending))
                .ToList();

            // Generate new rule matches for transactions without existing candidates
            var newRulesResult = transactionsWithoutRuleCandidates.Any() 
                ? await _rulesHandler.HandleAsync(transactionsWithoutRuleCandidates, cancellationToken)
                : new CategorizationResult();

            // Build comprehensive rule matches including both existing and new
            var allRuleMatches = await BuildComprehensiveRuleMatchDetailsAsync(
                existingRuleCandidates, newRulesResult, userId, cancellationToken);
            
            stopwatch.Stop();

            var totalExistingCandidates = existingRuleCandidates.Count(c => c.Status == CandidateStatus.Pending);
            var totalNewMatches = newRulesResult.AutoAppliedTransactions.Count + newRulesResult.Candidates.Count;
            var totalMatches = totalExistingCandidates + totalNewMatches;
            var totalUnmatched = allFilteredTransactions.Count - totalMatches;

            var result = new RuleAutoCategorizationResult
            {
                TotalTransactionsExamined = allFilteredTransactions.Count,
                TransactionsSkipped = 0, // We're now showing everything
                TransactionsMatched = totalMatches,
                TransactionsUnmatched = totalUnmatched,
                RuleMatches = allRuleMatches,
                Summary = BuildPreviewSummaryMessage(totalExistingCandidates, totalNewMatches, 
                    allFilteredTransactions.Count, totalUnmatched),
                IsPreview = true,
                ProcessingTime = stopwatch.Elapsed
            };

            _logger.LogInformation("Rule application preview completed for user {UserId}: {Matched}/{Total} transactions matched in {ElapsedMs}ms",
                userId, result.TransactionsMatched, result.TotalTransactionsExamined, result.ProcessingTime.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during rule application preview for user {UserId}", userId);
            stopwatch.Stop();
            
            return new RuleAutoCategorizationResult
            {
                Summary = "Error occurred during preview",
                IsPreview = true,
                ProcessingTime = stopwatch.Elapsed,
                Errors = { $"Preview failed: {ex.Message}" }
            };
        }
    }

    public async Task<RuleAutoCategorizationResult> ApplyRulesToFilteredTransactionsAsync(
        TransactionFilterCriteria filterCriteria,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting rule application for user {UserId}", userId);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Get filtered transactions without existing rule candidates
            var filteredTransactions = await GetFilteredTransactionsWithoutRuleCandidatesAsync(
                filterCriteria, userId, cancellationToken);

            if (!filteredTransactions.Any())
            {
                return new RuleAutoCategorizationResult
                {
                    TotalTransactionsExamined = 0,
                    TransactionsSkipped = 0,
                    TransactionsMatched = 0,
                    TransactionsUnmatched = 0,
                    Summary = "No transactions found matching the current filters, or all transactions already have rule candidates.",
                    IsPreview = false,
                    ProcessingTime = stopwatch.Elapsed
                };
            }

            // Apply rules to get actual matches
            var rulesResult = await _rulesHandler.HandleAsync(filteredTransactions, cancellationToken);
            
            // Apply high-confidence rule matches directly to transactions
            var appliedCount = 0;
            if (rulesResult.AutoAppliedTransactions.Any())
            {
                foreach (var categorizedTransaction in rulesResult.AutoAppliedTransactions)
                {
                    try
                    {
                        var transaction = await _transactionRepository.GetByIdAsync(
                            categorizedTransaction.Transaction.Id, userId);
                        
                        if (transaction != null)
                        {
                            // Apply the categorization directly to the transaction
                            transaction.CategoryId = categorizedTransaction.CategoryId;
                            transaction.MarkAsAutoCategorized(
                                CandidateMethod.Rule,
                                categorizedTransaction.ConfidenceScore,
                                $"RuleAutoCategorizationService-{userId}");

                            // CRITICAL FIX: Mark transaction as reviewed when applying rules
                            // This ensures transactions don't keep appearing after categorization
                            transaction.IsReviewed = true;

                            await _transactionRepository.UpdateAsync(transaction);
                            appliedCount++;

                            _logger.LogInformation("Applied rule categorization to transaction {TransactionId}: {CategoryName} ({Confidence}%)",
                                transaction.Id, categorizedTransaction.CategoryName,
                                Math.Round(categorizedTransaction.ConfidenceScore * 100));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to apply rule categorization to transaction {TransactionId}", 
                            categorizedTransaction.Transaction.Id);
                    }
                }
                
                _logger.LogInformation("Directly applied {AppliedCount} high-confidence rule categorizations for user {UserId}", 
                    appliedCount, userId);
            }
            
            // Create candidates in database for lower-confidence matched transactions
            if (rulesResult.Candidates.Any())
            {
                await _candidatesService.CreateCandidatesAsync(rulesResult.Candidates, cancellationToken);
                _logger.LogInformation("Created {CandidateCount} rule candidates for user {UserId}", 
                    rulesResult.Candidates.Count, userId);
            }

            // Count transactions we skipped (those with existing candidates) 
            var totalTransactionsInFilter = await GetFilteredTransactionCountAsync(filterCriteria, userId, cancellationToken);
            var skippedTransactions = totalTransactionsInFilter - filteredTransactions.Count;

            // Build detailed matches for result
            var ruleMatches = await BuildRuleMatchDetailsAsync(rulesResult, userId, cancellationToken);
            
            stopwatch.Stop();

            var result = new RuleAutoCategorizationResult
            {
                TotalTransactionsExamined = filteredTransactions.Count,
                TransactionsSkipped = skippedTransactions,
                TransactionsMatched = rulesResult.AutoAppliedTransactions.Count + rulesResult.Candidates.Count,
                TransactionsUnmatched = filteredTransactions.Count - (rulesResult.AutoAppliedTransactions.Count + rulesResult.Candidates.Count),
                RuleMatches = ruleMatches,
                Summary = BuildSummaryMessage(filteredTransactions.Count, skippedTransactions, 
                    rulesResult.AutoAppliedTransactions.Count + rulesResult.Candidates.Count, false),
                IsPreview = false,
                ProcessingTime = stopwatch.Elapsed,
                ProcessedTransactionIds = rulesResult.AutoAppliedTransactions.Select(t => t.Transaction.Id)
                    .Concat(rulesResult.Candidates.Select(c => c.TransactionId)).ToList()
            };

            _logger.LogInformation("Rule application completed for user {UserId}: {Matched}/{Total} transactions matched, {Candidates} candidates created in {ElapsedMs}ms",
                userId, result.TransactionsMatched, result.TotalTransactionsExamined, rulesResult.Candidates.Count, result.ProcessingTime.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during rule application for user {UserId}", userId);
            stopwatch.Stop();
            
            return new RuleAutoCategorizationResult
            {
                Summary = "Error occurred during rule application",
                IsPreview = false,
                ProcessingTime = stopwatch.Elapsed,
                Errors = { $"Rule application failed: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// Gets filtered transactions that don't already have rule candidates
    /// This prevents duplicate rule candidate creation
    /// </summary>
    private async Task<IList<Transaction>> GetFilteredTransactionsWithoutRuleCandidatesAsync(
        TransactionFilterCriteria criteria, 
        Guid userId, 
        CancellationToken cancellationToken)
    {
        // Convert criteria to GetTransactionsQuery for the repository
        var query = new GetTransactionsQuery
        {
            UserId = userId,
            Page = 1,
            PageSize = int.MaxValue, // Get all transactions matching criteria
            StartDate = criteria.StartDate,
            EndDate = criteria.EndDate,
            MinAmount = criteria.MinAmount,
            MaxAmount = criteria.MaxAmount,
            TransactionType = criteria.TransactionType,
            SearchTerm = criteria.SearchText,
            IsReviewed = criteria.OnlyUnreviewed ? false : null,
            IncludeTransfers = !criteria.ExcludeTransfers,
            SortBy = "TransactionDate",
            SortDirection = "asc"
        };

        // Add account filter if specified
        if (criteria.AccountIds.Any())
        {
            // Repository expects single AccountId, take first one for now
            // TODO: Enhance repository to support multiple account IDs
            query.AccountId = criteria.AccountIds.First();
        }

        // Get filtered transactions from repository
        var (allTransactions, _) = await _transactionRepository.GetFilteredAsync(query);
        var transactions = allTransactions.ToList();

        // Lets remove the filter for existing rule candidates for now.
        //
        // Get transaction IDs that already have rule candidates
        // var transactionIds = transactions.Select(t => t.Id).ToList();
        // var transactionIdsWithRuleCandidates = await _candidatesRepository
        //     .GetCandidatesForTransactionsByMethodAsync(transactionIds, "Rule", cancellationToken);
        // var idsWithRuleCandidates = transactionIdsWithRuleCandidates
        //     .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Pending)
        //     .Select(c => c.TransactionId)
        //     .ToHashSet();
        //
        // // Filter out transactions that already have rule candidates
        // var filteredTransactions = transactions
        //     .Where(t => !idsWithRuleCandidates.Contains(t.Id))
        //     .ToList();
        //
        // _logger.LogDebug("Found {Count} transactions without existing rule candidates for user {UserId} (filtered from {Total})", 
        //     filteredTransactions.Count, userId, transactions.Count);

        return transactions;
    }

    /// <summary>
    /// Gets total count of transactions matching filter (including those with candidates)
    /// Used to calculate how many transactions were skipped
    /// </summary>
    private async Task<int> GetFilteredTransactionCountAsync(
        TransactionFilterCriteria criteria,
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Convert criteria to GetTransactionsQuery for the repository
        var query = new GetTransactionsQuery
        {
            UserId = userId,
            Page = 1,
            PageSize = int.MaxValue, // Get all transactions matching criteria
            StartDate = criteria.StartDate,
            EndDate = criteria.EndDate,
            MinAmount = criteria.MinAmount,
            MaxAmount = criteria.MaxAmount,
            TransactionType = criteria.TransactionType,
            SearchTerm = criteria.SearchText,
            IsReviewed = criteria.OnlyUnreviewed ? false : null,
            IncludeTransfers = !criteria.ExcludeTransfers,
            SortBy = "TransactionDate",
            SortDirection = "asc"
        };

        // Add account filter if specified
        if (criteria.AccountIds.Any())
        {
            // Repository expects single AccountId, take first one for now
            // TODO: Enhance repository to support multiple account IDs
            query.AccountId = criteria.AccountIds.First();
        }

        var (_, totalCount) = await _transactionRepository.GetFilteredAsync(query);
        return totalCount;
    }

    // ApplyFilterCriteria method removed - now using GetTransactionsQuery to leverage existing repository filtering logic

    /// <summary>
    /// Builds detailed rule match information for user review
    /// </summary>
    private async Task<List<RuleMatchDetail>> BuildRuleMatchDetailsAsync(
        CategorizationResult rulesResult,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var details = new List<RuleMatchDetail>();

        // Process auto-applied transactions
        foreach (var applied in rulesResult.AutoAppliedTransactions)
        {
            // Extract rule information from metadata
            var ruleId = ExtractRuleIdFromMetadata(applied.Metadata);
            var ruleName = ExtractRuleNameFromMetadata(applied.Metadata) ?? "Rule Match";
            var rulePattern = ExtractRulePatternFromMetadata(applied.Metadata) ?? "";

            details.Add(new RuleMatchDetail
            {
                TransactionId = applied.Transaction.Id,
                TransactionDescription = applied.Transaction.Description ?? "",
                TransactionAmount = applied.Transaction.Amount,
                TransactionDate = applied.Transaction.TransactionDate,
                AccountName = applied.Transaction.Account?.Name ?? "",
                CategoryId = applied.CategoryId,
                CategoryName = applied.CategoryName ?? "",
                ConfidenceScore = applied.ConfidenceScore,
                WouldChangeCategory = applied.Transaction.CategoryId != applied.CategoryId,
                CurrentCategoryName = applied.Transaction.Category?.Name,
                RuleId = ruleId,
                RuleName = ruleName,
                RulePattern = rulePattern
            });
        }

        // Get transaction IDs for batch retrieval with includes
        var transactionIds = rulesResult.Candidates.Select(c => c.TransactionId).Distinct().ToArray();
        
        // Batch retrieve transactions with their categories loaded
        var transactions = await _transactionRepository.GetTransactionsByIdsAsync(
            transactionIds, 
            userId,
            cancellationToken);
        var transactionDict = transactions.ToDictionary(t => t.Id);

        // Process candidates using navigation properties
        foreach (var candidate in rulesResult.Candidates)
        {
            if (!transactionDict.TryGetValue(candidate.TransactionId, out var transaction))
            {
                continue; // Skip if transaction not found
            }

            // Extract rule information from candidate metadata
            var ruleId = ExtractRuleIdFromCandidateMetadata(candidate.Metadata);
            var ruleName = ExtractRuleNameFromCandidateMetadata(candidate.Metadata) ?? "Rule Match";
            var rulePattern = ExtractRulePatternFromCandidateMetadata(candidate.Metadata) ?? "";

            // Use navigation property for category name - this is the key fix!
            var categoryName = candidate.Category?.Name ?? "Unknown Category";

            details.Add(new RuleMatchDetail
            {
                TransactionId = candidate.TransactionId,
                TransactionDescription = transaction.Description ?? "",
                TransactionAmount = transaction.Amount,
                TransactionDate = transaction.TransactionDate,
                AccountName = transaction.Account?.Name ?? "",
                CategoryId = candidate.CategoryId,
                CategoryName = categoryName, // Using navigation property
                ConfidenceScore = candidate.ConfidenceScore,
                WouldChangeCategory = transaction.CategoryId != candidate.CategoryId,
                CurrentCategoryName = transaction.Category?.Name,
                RuleId = ruleId,
                RuleName = ruleName,
                RulePattern = rulePattern
            });
        }

        return details;
    }

    /// <summary>
    /// Extract rule ID from CategorizedTransaction metadata
    /// </summary>
    private int ExtractRuleIdFromMetadata(Dictionary<string, object>? metadata)
    {
        if (metadata != null && metadata.TryGetValue("RuleId", out var ruleIdObj))
        {
            if (int.TryParse(ruleIdObj?.ToString(), out var ruleId))
                return ruleId;
        }
        return 0;
    }

    /// <summary>
    /// Extract rule name from CategorizedTransaction metadata
    /// </summary>
    private string? ExtractRuleNameFromMetadata(Dictionary<string, object>? metadata)
    {
        if (metadata != null && metadata.TryGetValue("RuleName", out var ruleNameObj))
        {
            return ruleNameObj?.ToString();
        }
        return null;
    }

    /// <summary>
    /// Extract rule pattern from CategorizedTransaction metadata
    /// </summary>
    private string? ExtractRulePatternFromMetadata(Dictionary<string, object>? metadata)
    {
        if (metadata != null && metadata.TryGetValue("RulePattern", out var rulePatternObj))
        {
            return rulePatternObj?.ToString();
        }
        return null;
    }

    /// <summary>
    /// Extract rule ID from CategorizationCandidate metadata JSON
    /// </summary>
    private int ExtractRuleIdFromCandidateMetadata(string? metadataJson)
    {
        if (string.IsNullOrEmpty(metadataJson))
            return 0;

        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            // Try camelCase first (how it's serialized), then PascalCase (fallback)
            if (doc.RootElement.TryGetProperty("ruleId", out var ruleIdElement) ||
                doc.RootElement.TryGetProperty("RuleId", out ruleIdElement))
            {
                if (ruleIdElement.TryGetInt32(out var ruleId))
                    return ruleId;
            }
        }
        catch (JsonException)
        {
            // Invalid JSON, return default
        }
        return 0;
    }

    /// <summary>
    /// Extract rule name from CategorizationCandidate metadata JSON
    /// </summary>
    private string? ExtractRuleNameFromCandidateMetadata(string? metadataJson)
    {
        if (string.IsNullOrEmpty(metadataJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            // Try camelCase first (how it's serialized), then PascalCase (fallback)
            if (doc.RootElement.TryGetProperty("ruleName", out var ruleNameElement) ||
                doc.RootElement.TryGetProperty("RuleName", out ruleNameElement))
            {
                return ruleNameElement.GetString();
            }
        }
        catch (JsonException)
        {
            // Invalid JSON, return null
        }
        return null;
    }

    /// <summary>
    /// Extract rule pattern from CategorizationCandidate metadata JSON
    /// </summary>
    private string? ExtractRulePatternFromCandidateMetadata(string? metadataJson)
    {
        if (string.IsNullOrEmpty(metadataJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            // Try camelCase first (how it's serialized), then PascalCase (fallback)
            if (doc.RootElement.TryGetProperty("rulePattern", out var rulePatternElement) ||
                doc.RootElement.TryGetProperty("RulePattern", out rulePatternElement))
            {
                return rulePatternElement.GetString();
            }
        }
        catch (JsonException)
        {
            // Invalid JSON, return null
        }
        return null;
    }

    /// <summary>
    /// Builds a summary message for user display
    /// </summary>
    private string BuildSummaryMessage(int examined, int skipped, int matched, bool isPreview)
    {
        var action = isPreview ? "would be categorized" : "categorized";
        var message = $"{matched} of {examined} transactions {action}";
        
        if (skipped > 0)
        {
            message += $" ({skipped} skipped - already have rule suggestions)";
        }
        
        return message;
    }

    /// <summary>
    /// Gets ALL filtered transactions (including those with existing rule candidates)
    /// This is different from GetFilteredTransactionsWithoutRuleCandidatesAsync which excludes them
    /// </summary>
    private async Task<IList<Transaction>> GetAllFilteredTransactionsAsync(
        TransactionFilterCriteria criteria, 
        Guid userId, 
        CancellationToken cancellationToken)
    {
        // Convert criteria to GetTransactionsQuery for the repository
        var query = new GetTransactionsQuery
        {
            UserId = userId,
            Page = 1,
            PageSize = int.MaxValue, // Get all transactions matching criteria
            StartDate = criteria.StartDate,
            EndDate = criteria.EndDate,
            MinAmount = criteria.MinAmount,
            MaxAmount = criteria.MaxAmount,
            TransactionType = criteria.TransactionType,
            SearchTerm = criteria.SearchText,
            IsReviewed = criteria.OnlyUnreviewed ? false : null,
            IncludeTransfers = !criteria.ExcludeTransfers,
            SortBy = "TransactionDate",
            SortDirection = "asc"
        };

        // Add account filter if specified
        if (criteria.AccountIds.Any())
        {
            // Repository expects single AccountId, take first one for now
            // TODO: Enhance repository to support multiple account IDs
            query.AccountId = criteria.AccountIds.First();
        }

        // Get filtered transactions from repository
        var (allTransactions, _) = await _transactionRepository.GetFilteredAsync(query);
        return allTransactions.ToList();
    }

    /// <summary>
    /// Builds comprehensive rule match details including both existing candidates and new matches
    /// </summary>
    private async Task<List<RuleMatchDetail>> BuildComprehensiveRuleMatchDetailsAsync(
        IEnumerable<CategorizationCandidate> existingCandidates,
        CategorizationResult newRulesResult,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var details = new List<RuleMatchDetail>();

        // Add existing rule candidates
        var existingPendingCandidates = existingCandidates
            .Where(c => c.Status == CandidateStatus.Pending)
            .ToList();

        if (existingPendingCandidates.Any())
        {
            // Get transaction details for existing candidates
            var transactionIds = existingPendingCandidates.Select(c => c.TransactionId).Distinct().ToArray();
            var transactions = await _transactionRepository.GetTransactionsByIdsAsync(
                transactionIds, userId, cancellationToken);
            var transactionDict = transactions.ToDictionary(t => t.Id);

            foreach (var candidate in existingPendingCandidates)
            {
                if (!transactionDict.TryGetValue(candidate.TransactionId, out var transaction))
                    continue;

                var ruleId = ExtractRuleIdFromCandidateMetadata(candidate.Metadata);
                var ruleName = ExtractRuleNameFromCandidateMetadata(candidate.Metadata) ?? "Existing Rule";
                var rulePattern = ExtractRulePatternFromCandidateMetadata(candidate.Metadata) ?? "";
                var categoryName = candidate.Category?.Name ?? "Unknown Category";

                details.Add(new RuleMatchDetail
                {
                    TransactionId = candidate.TransactionId,
                    TransactionDescription = transaction.Description ?? "",
                    TransactionAmount = transaction.Amount,
                    TransactionDate = transaction.TransactionDate,
                    AccountName = transaction.Account?.Name ?? "",
                    CategoryId = candidate.CategoryId,
                    CategoryName = categoryName,
                    ConfidenceScore = candidate.ConfidenceScore,
                    WouldChangeCategory = transaction.CategoryId != candidate.CategoryId,
                    CurrentCategoryName = transaction.Category?.Name,
                    RuleId = ruleId,
                    RuleName = ruleName,
                    RulePattern = rulePattern,
                    IsExistingCandidate = true,
                    CandidateId = candidate.Id,
                    IsSelected = true, // Default to selected
                    CanAutoApply = candidate.CanAutoApply()
                });
            }
        }

        // Add new rule matches (using existing logic)
        var newMatches = await BuildRuleMatchDetailsAsync(newRulesResult, userId, cancellationToken);
        foreach (var match in newMatches)
        {
            match.IsExistingCandidate = false;
            match.CandidateId = null;
            match.IsSelected = true; // Default to selected
            details.Add(match);
        }

        return details.OrderBy(d => d.TransactionDate).ThenBy(d => d.TransactionId).ToList();
    }

    /// <summary>
    /// Builds a summary message for preview showing existing and new matches
    /// </summary>
    private string BuildPreviewSummaryMessage(int existingCandidates, int newMatches, int totalExamined, int unmatched)
    {
        if (existingCandidates == 0 && newMatches == 0)
        {
            return $"No rule matches found for {totalExamined} transactions";
        }

        var parts = new List<string>();
        if (existingCandidates > 0)
        {
            parts.Add($"{existingCandidates} existing rule suggestions");
        }
        if (newMatches > 0)
        {
            parts.Add($"{newMatches} new rule matches");
        }

        var message = $"{string.Join(" + ", parts)} = {existingCandidates + newMatches} total rule suggestions";
        if (unmatched > 0)
        {
            message += $" ({unmatched} transactions have no rule matches)";
        }

        return message;
    }

    /// <summary>
    /// Applies selected rule matches to transactions
    /// Allows users to choose which rule suggestions to apply
    /// </summary>
    public async Task<RuleAutoCategorizationResult> ApplySelectedRuleMatchesAsync(
        List<RuleMatchDetail> selectedMatches,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting selective rule application for user {UserId} with {SelectedCount} matches", 
            userId, selectedMatches.Count);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!selectedMatches.Any())
            {
                return new RuleAutoCategorizationResult
                {
                    Summary = "No rule matches selected for application",
                    IsPreview = false,
                    ProcessingTime = stopwatch.Elapsed
                };
            }

            var appliedCount = 0;
            var errors = new List<string>();

            // Separate existing candidates from new matches
            var existingCandidates = selectedMatches.Where(m => m.IsExistingCandidate && m.CandidateId.HasValue).ToList();
            var newMatches = selectedMatches.Where(m => !m.IsExistingCandidate).ToList();

            // Apply existing candidates using the candidates service
            if (existingCandidates.Any())
            {
                var candidateIds = existingCandidates.Select(c => c.CandidateId!.Value).ToList();
                var batchResult = await _candidatesService.ApplyCandidatesBatchAsync(
                    candidateIds, $"RuleAutoCategorizationService-{userId}", userId, cancellationToken);
                
                appliedCount += batchResult.SuccessfulCount;
                errors.AddRange(batchResult.Errors);
                
                _logger.LogInformation("Applied {Count} existing rule candidates", batchResult.SuccessfulCount);
            }

            // Apply new matches directly to transactions
            foreach (var match in newMatches)
            {
                try
                {
                    var transaction = await _transactionRepository.GetByIdAsync(match.TransactionId, userId);
                    if (transaction != null)
                    {
                        transaction.CategoryId = match.CategoryId;
                        transaction.MarkAsAutoCategorized(
                            CandidateMethod.Rule,
                            match.ConfidenceScore,
                            $"RuleAutoCategorizationService-{userId}");

                        // CRITICAL FIX: Mark transaction as reviewed when user explicitly applies a rule
                        // This ensures transactions don't keep appearing after categorization
                        transaction.IsReviewed = true;

                        await _transactionRepository.UpdateAsync(transaction);
                        appliedCount++;

                        _logger.LogInformation("Applied new rule match to transaction {TransactionId}: {CategoryName} ({Confidence}%)",
                            transaction.Id, match.CategoryName, Math.Round(match.ConfidenceScore * 100));
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to apply rule to transaction {match.TransactionId}: {ex.Message}");
                    _logger.LogError(ex, "Failed to apply new rule match to transaction {TransactionId}", match.TransactionId);
                }
            }

            stopwatch.Stop();

            var result = new RuleAutoCategorizationResult
            {
                TotalTransactionsExamined = selectedMatches.Count,
                TransactionsMatched = appliedCount,
                TransactionsUnmatched = selectedMatches.Count - appliedCount,
                Summary = BuildSelectiveApplySummaryMessage(appliedCount, selectedMatches.Count, errors.Count),
                IsPreview = false,
                ProcessingTime = stopwatch.Elapsed,
                ProcessedTransactionIds = selectedMatches.Select(m => m.TransactionId).ToList(),
                Errors = errors
            };

            _logger.LogInformation("Selective rule application completed for user {UserId}: {Applied}/{Selected} applied in {ElapsedMs}ms",
                userId, appliedCount, selectedMatches.Count, result.ProcessingTime.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during selective rule application for user {UserId}", userId);
            stopwatch.Stop();
            
            return new RuleAutoCategorizationResult
            {
                Summary = "Error occurred during selective rule application",
                IsPreview = false,
                ProcessingTime = stopwatch.Elapsed,
                Errors = { $"Selective application failed: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// Builds a summary message for selective application
    /// </summary>
    private string BuildSelectiveApplySummaryMessage(int applied, int selected, int errors)
    {
        if (applied == selected && errors == 0)
        {
            return $"Successfully applied {applied} rule categorizations";
        }
        
        var message = $"Applied {applied} of {selected} selected rule categorizations";
        if (errors > 0)
        {
            message += $" ({errors} failed)";
        }
        
        return message;
    }
}