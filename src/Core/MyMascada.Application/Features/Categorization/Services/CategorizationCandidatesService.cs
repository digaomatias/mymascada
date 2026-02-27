using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Categorization.Services;

/// <summary>
/// Service for managing categorization candidates and their application to transactions
/// </summary>
public class CategorizationCandidatesService : ICategorizationCandidatesService
{
    private readonly ICategorizationCandidatesRepository _candidatesRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<CategorizationCandidatesService> _logger;

    public CategorizationCandidatesService(
        ICategorizationCandidatesRepository candidatesRepository,
        ITransactionRepository transactionRepository,
        ILogger<CategorizationCandidatesService> logger)
    {
        _candidatesRepository = candidatesRepository;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    public async Task<Dictionary<int, List<CategorizationCandidate>>> GetPendingCandidatesForTransactionsAsync(
        IEnumerable<int> transactionIds, CancellationToken cancellationToken = default)
    {
        return await _candidatesRepository.GetCandidatesGroupedByTransactionAsync(transactionIds, cancellationToken);
    }

    public async Task<IEnumerable<CategorizationCandidate>> CreateCandidatesAsync(
        IEnumerable<CategorizationCandidate> candidates, CancellationToken cancellationToken = default)
    {
        var candidatesList = candidates.ToList();
        if (!candidatesList.Any())
            return candidatesList;

        _logger.LogDebug("Processing {CandidateCount} categorization candidates", candidatesList.Count);
        
        // Extract unique transaction IDs
        var transactionIds = candidatesList.Select(c => c.TransactionId).Distinct().ToList();
        
        // Check which transactions are already categorized
        var categorizedTransactions = await _transactionRepository.GetCategorizedTransactionIdsAsync(transactionIds);
        
        // Get existing pending candidates to check for exact duplicates (same TransactionId + CategoryId + Method)
        var existingCandidates = await _candidatesRepository.GetPendingCandidatesForTransactionsAsync(
            transactionIds, cancellationToken);
        
        // Create a set of existing combinations for fast lookup
        var existingCombinations = new HashSet<string>();
        foreach (var existing in existingCandidates)
        {
            var key = $"{existing.TransactionId}_{existing.CategoryId}_{existing.CategorizationMethod}";
            existingCombinations.Add(key);
        }
        
        // Filter out candidates that are duplicates or for categorized transactions
        var validCandidates = new List<CategorizationCandidate>();
        var duplicateCount = 0;
        var categorizedCount = 0;
        
        foreach (var candidate in candidatesList)
        {
            // Skip if transaction is already categorized
            if (categorizedTransactions.Contains(candidate.TransactionId))
            {
                categorizedCount++;
                continue;
            }
            
            // Check for exact duplicate (same TransactionId + CategoryId + Method)
            var candidateKey = $"{candidate.TransactionId}_{candidate.CategoryId}_{candidate.CategorizationMethod}";
            if (existingCombinations.Contains(candidateKey))
            {
                duplicateCount++;
                continue;
            }
            
            validCandidates.Add(candidate);
            // Add to set to prevent duplicates within the current batch
            existingCombinations.Add(candidateKey);
        }
        
        if (validCandidates.Count < candidatesList.Count)
        {
            _logger.LogInformation(
                "Filtered out {FilteredCount} candidates: {DuplicateCount} exact duplicates, " +
                "{CategorizedCount} transactions already categorized",
                candidatesList.Count - validCandidates.Count,
                duplicateCount,
                categorizedCount);
        }
        
        if (!validCandidates.Any())
        {
            _logger.LogDebug("No valid candidates to create after filtering");
            return new List<CategorizationCandidate>();
        }
        
        _logger.LogDebug("Creating {CandidateCount} valid categorization candidates", validCandidates.Count);
        
        return await _candidatesRepository.AddCandidatesBatchAsync(validCandidates, cancellationToken);
    }

    public async Task<bool> ApplyCandidateAsync(
        int candidateId, string appliedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var candidate = await _candidatesRepository.GetByIdAsync(candidateId, cancellationToken);
            if (candidate == null)
            {
                _logger.LogWarning("Candidate {CandidateId} not found", candidateId);
                return false;
            }

            if (candidate.Status != CandidateStatus.Pending)
            {
                _logger.LogWarning("Candidate {CandidateId} is not pending (status: {Status})", candidateId, candidate.Status);
                return false;
            }

            // Apply the categorization to the transaction
            var transaction = candidate.Transaction;
            transaction.CategoryId = candidate.CategoryId;
            transaction.MarkAsAutoCategorized(candidate.CategorizationMethod, candidate.ConfidenceScore, appliedBy);

            await _transactionRepository.UpdateAsync(transaction);

            // Mark candidate as applied
            candidate.MarkAsApplied(appliedBy);
            await _candidatesRepository.UpdateCandidateAsync(candidate, cancellationToken);

            _logger.LogDebug("Successfully applied candidate {CandidateId} to transaction {TransactionId}", 
                candidateId, transaction.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying candidate {CandidateId}", candidateId);
            return false;
        }
    }

    public async Task<bool> RejectCandidateAsync(
        int candidateId, string rejectedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var candidate = await _candidatesRepository.GetByIdAsync(candidateId, cancellationToken);
            if (candidate == null)
            {
                _logger.LogWarning("Candidate {CandidateId} not found", candidateId);
                return false;
            }

            if (candidate.Status != CandidateStatus.Pending)
            {
                _logger.LogWarning("Candidate {CandidateId} is not pending (status: {Status})", candidateId, candidate.Status);
                return false;
            }

            candidate.MarkAsRejected(rejectedBy);
            await _candidatesRepository.UpdateCandidateAsync(candidate, cancellationToken);

            _logger.LogDebug("Successfully rejected candidate {CandidateId}", candidateId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting candidate {CandidateId}", candidateId);
            return false;
        }
    }

    public async Task<BatchCandidateResult> ApplyCandidatesBatchAsync(
        IEnumerable<int> candidateIds, string appliedBy, Guid userId, CancellationToken cancellationToken = default)
    {
        var result = new BatchCandidateResult();
        var ids = candidateIds.ToList();

        if (!ids.Any())
            return result;

        try
        {
            // Get all candidates with their related data in a single query to avoid tracking conflicts
            var candidates = new List<CategorizationCandidate>();
            foreach (var id in ids)
            {
                var candidate = await _candidatesRepository.GetByIdAsync(id, cancellationToken);
                if (candidate != null && candidate.Status == CandidateStatus.Pending)
                {
                    candidates.Add(candidate);
                }
                else
                {
                    result.FailedCount++;
                    result.Errors.Add($"Candidate {id} not found or not pending");
                }
            }

            if (!candidates.Any())
            {
                _logger.LogDebug("No valid candidates to apply after validation");
                return result;
            }

            // Apply categorization to all transactions in a single bulk operation
            var candidateTransactionUpdates = candidates.Select(c => new
            {
                TransactionId = c.TransactionId,
                CategoryId = c.CategoryId,
                CategorizationMethod = c.CategorizationMethod,
                ConfidenceScore = c.ConfidenceScore,
                CandidateId = c.Id
            }).ToList();

            var transactionIds = candidateTransactionUpdates.Select(u => u.TransactionId).ToList();
            var successfulCandidateIds = candidateTransactionUpdates.Select(u => u.CandidateId).ToList();

            try
            {
                // Update all transactions in a single bulk operation
                await _transactionRepository.BulkUpdateCategorizationAsync(
                    candidateTransactionUpdates.Select(u => new
                    {
                        u.TransactionId,
                        u.CategoryId,
                        CategorizationMethod = u.CategorizationMethod,
                        ConfidenceScore = u.ConfidenceScore,
                        AutoCategorizedBy = appliedBy,
                        AutoCategorizedAt = DateTime.UtcNow,
                        IsReviewed = true
                    }),
                    userId,
                    cancellationToken);

                // Mark all candidates as applied in a single bulk operation
                await _candidatesRepository.BulkMarkCandidatesAsAppliedAsync(successfulCandidateIds, appliedBy, cancellationToken);

                result.SuccessfulCount = candidates.Count;
                _logger.LogDebug("Successfully bulk applied {Count} candidates", result.SuccessfulCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk categorization update: {Message}", ex.Message);
                result.Errors.Add($"Failed to apply categorization: {ex.Message}");
                result.FailedCount = candidates.Count;
            }

            _logger.LogDebug("Batch applied {SuccessfulCount} candidates, {FailedCount} failed", 
                result.SuccessfulCount, result.FailedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch apply candidates");
            result.Errors.Add($"Batch operation failed: {ex.Message}");
            result.FailedCount = ids.Count;
            result.SuccessfulCount = 0;
        }

        return result;
    }

    public async Task<BatchCandidateResult> RejectCandidatesBatchAsync(
        IEnumerable<int> candidateIds, string rejectedBy, CancellationToken cancellationToken = default)
    {
        var result = new BatchCandidateResult();
        var ids = candidateIds.ToList();

        if (!ids.Any())
            return result;

        try
        {
            await _candidatesRepository.MarkCandidatesAsRejectedBatchAsync(ids, rejectedBy, cancellationToken);
            result.SuccessfulCount = ids.Count;

            _logger.LogDebug("Batch rejected {Count} candidates", ids.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch reject candidates");
            result.Errors.Add($"Batch operation failed: {ex.Message}");
            result.FailedCount = ids.Count;
        }

        return result;
    }

    public async Task<AutoApplyResult> AutoApplyHighConfidenceCandidatesAsync(
        IEnumerable<CategorizationCandidate> candidates,
        string appliedBy,
        decimal confidenceThreshold = 0.95m,
        CancellationToken cancellationToken = default)
    {
        var result = new AutoApplyResult();
        var candidatesList = candidates.ToList();

        if (!candidatesList.Any())
            return result;

        // Filter for high-confidence candidates that can be auto-applied
        var autoApplyCandidates = candidatesList
            .Where(c => c.CanAutoApply(confidenceThreshold))
            .ToList();

        result.RemainingCandidatesCount = candidatesList.Count - autoApplyCandidates.Count;

        if (!autoApplyCandidates.Any())
        {
            _logger.LogDebug("No candidates meet auto-apply criteria (threshold: {Threshold})", confidenceThreshold);
            return result;
        }

        _logger.LogDebug("Auto-applying {AutoApplyCount} of {TotalCount} candidates with confidence >= {Threshold}",
            autoApplyCandidates.Count, candidatesList.Count, confidenceThreshold);

        // First, save all candidates to database
        var savedCandidates = await CreateCandidatesAsync(candidatesList, cancellationToken);
        var savedAutoApplyCandidates = savedCandidates.Where(c => c.CanAutoApply(confidenceThreshold)).ToList();

        // Apply the high-confidence ones
        foreach (var candidate in savedAutoApplyCandidates)
        {
            try
            {
                var success = await ApplyCandidateAsync(candidate.Id, appliedBy, cancellationToken);
                if (success)
                {
                    result.AppliedCount++;
                    result.AppliedCandidateIds.Add(candidate.Id);
                    result.AppliedTransactionIds.Add(candidate.TransactionId);
                }
                else
                {
                    result.Errors.Add($"Failed to apply candidate {candidate.Id}");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error applying candidate {candidate.Id}: {ex.Message}");
                _logger.LogError(ex, "Error auto-applying candidate {CandidateId}", candidate.Id);
            }
        }

        _logger.LogDebug("Auto-applied {AppliedCount} candidates successfully", result.AppliedCount);
        return result;
    }

    public async Task<CategorizationCandidateStats> GetCandidateStatsAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        return await _candidatesRepository.GetCandidateStatsAsync(userId, cancellationToken);
    }

    public async Task<IEnumerable<AiCategorySuggestion>> ConvertCandidatesToAiSuggestionsAsync(
        IEnumerable<CategorizationCandidate> candidates, CancellationToken cancellationToken = default)
    {
        return await Task.FromResult(candidates.Select(c => new AiCategorySuggestion
        {
            CategoryId = c.CategoryId,
            CategoryName = c.Category?.Name ?? "Unknown",
            Confidence = c.ConfidenceScore,
            Reasoning = c.Reasoning ?? "",
            Method = c.CategorizationMethod,
            CandidateId = c.Id,
            CanAutoApply = c.CanAutoApply()
        }));
    }
}