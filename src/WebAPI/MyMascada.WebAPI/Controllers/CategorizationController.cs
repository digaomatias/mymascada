using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Common.Models;
using MyMascada.Application.Features.Categorization.Services;
using MyMascada.Application.Features.Transactions.Commands;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategorizationController : ControllerBase
{
    private readonly ICategorizationPipeline _categorizationPipeline;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategorizationCandidatesService _candidatesService;
    private readonly ICategorizationCandidatesRepository _candidatesRepository;
    private readonly ITransactionQueryService _transactionQueryService;
    private readonly IRuleAutoCategorizationService _ruleAutoCategorizationService;
    private readonly IMediator _mediator;

    public CategorizationController(
        ICategorizationPipeline categorizationPipeline,
        ITransactionRepository transactionRepository,
        ICategorizationCandidatesService candidatesService,
        ICategorizationCandidatesRepository candidatesRepository,
        ITransactionQueryService transactionQueryService,
        IRuleAutoCategorizationService ruleAutoCategorizationService,
        IMediator mediator)
    {
        _categorizationPipeline = categorizationPipeline;
        _transactionRepository = transactionRepository;
        _candidatesService = candidatesService;
        _candidatesRepository = candidatesRepository;
        _transactionQueryService = transactionQueryService;
        _ruleAutoCategorizationService = ruleAutoCategorizationService;
        _mediator = mediator;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token");
        }
        return userId;
    }

    /// <summary>
    /// Manually triggers the categorization pipeline for specific transactions
    /// </summary>
    [HttpPost("process")]
    public async Task<IActionResult> ProcessTransactions(
        [FromBody] int[] transactionIds, 
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        
        if (transactionIds == null || !transactionIds.Any())
        {
            return BadRequest("Transaction IDs are required");
        }

        if (transactionIds.Length > 100)
        {
            return BadRequest("Maximum 100 transactions can be processed at once");
        }

        try
        {
            // Get transactions for the user
            var transactions = await _transactionRepository.GetTransactionsByIdsAsync(transactionIds, userId, cancellationToken);
            var transactionsList = transactions.ToList();

            if (!transactionsList.Any())
            {
                return NotFound("No transactions found for the provided IDs");
            }

            // Process through categorization pipeline
            var result = await _categorizationPipeline.ProcessAsync(transactionsList, cancellationToken);

            // Apply categorizations to the database
            foreach (var categorizedTransaction in result.CategorizedTransactions)
            {
                categorizedTransaction.Transaction.CategoryId = categorizedTransaction.CategoryId;
                await _transactionRepository.UpdateAsync(categorizedTransaction.Transaction);
            }

            // Return results
            var response = new
            {
                TotalTransactions = transactionsList.Count,
                CategorizedTransactions = result.CategorizedTransactions.Count,
                RemainingTransactions = result.RemainingTransactions.Count,
                ProcessingTimeMs = result.Metrics.ProcessingTime.TotalMilliseconds,
                Metrics = new
                {
                    ProcessedByRules = result.Metrics.ProcessedByRules,
                    ProcessedByML = result.Metrics.ProcessedByML,
                    ProcessedByLLM = result.Metrics.ProcessedByLLM,
                    SuccessRate = result.Metrics.SuccessRate,
                    EstimatedCostSavings = result.Metrics.EstimatedCostSavings
                },
                Categories = result.CategorizedTransactions.GroupBy(ct => ct.CategoryName)
                    .Select(g => new { Category = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count),
                Errors = result.Errors
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error processing transactions");
        }
    }

    /// <summary>
    /// Processes all uncategorized transactions for the current user
    /// </summary>
    [HttpPost("process-uncategorized")]
    public async Task<IActionResult> ProcessUncategorizedTransactions(
        [FromQuery] int maxTransactions = 500,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        try
        {
            // Get uncategorized transactions
            var uncategorizedTransactions = await _transactionRepository.GetUncategorizedTransactionsAsync(
                userId, maxTransactions, cancellationToken);
            var transactionsList = uncategorizedTransactions.ToList();

            if (!transactionsList.Any())
            {
                return Ok(new { Message = "No uncategorized transactions found", TotalTransactions = 0 });
            }

            // Process through categorization pipeline
            var result = await _categorizationPipeline.ProcessAsync(transactionsList, cancellationToken);

            // Apply categorizations to the database
            foreach (var categorizedTransaction in result.CategorizedTransactions)
            {
                categorizedTransaction.Transaction.CategoryId = categorizedTransaction.CategoryId;
                await _transactionRepository.UpdateAsync(categorizedTransaction.Transaction);
            }

            // Return results
            var response = new
            {
                TotalTransactions = transactionsList.Count,
                CategorizedTransactions = result.CategorizedTransactions.Count,
                RemainingTransactions = result.RemainingTransactions.Count,
                ProcessingTimeMs = result.Metrics.ProcessingTime.TotalMilliseconds,
                Metrics = new
                {
                    ProcessedByRules = result.Metrics.ProcessedByRules,
                    ProcessedByML = result.Metrics.ProcessedByML,
                    ProcessedByLLM = result.Metrics.ProcessedByLLM,
                    SuccessRate = result.Metrics.SuccessRate,
                    EstimatedCostSavings = result.Metrics.EstimatedCostSavings
                },
                Categories = result.CategorizedTransactions.GroupBy(ct => ct.CategoryName)
                    .Select(g => new { Category = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count),
                Errors = result.Errors
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error processing uncategorized transactions");
        }
    }

    /// <summary>
    /// Bulk categorizes transactions using only LLM processing
    /// This bypasses the pipeline and creates candidates directly from LLM suggestions
    /// </summary>
    [HttpPost("bulk-categorize-with-llm")]
    public async Task<IActionResult> BulkCategorizeWithLlm(
        [FromBody] int[] transactionIds,
        [FromQuery] int maxBatchSize = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (transactionIds == null || !transactionIds.Any())
        {
            return BadRequest("Transaction IDs are required");
        }

        if (transactionIds.Length > 500)
        {
            return BadRequest("Maximum 500 transactions can be processed at once");
        }

        try
        {
            var command = new BulkCategorizeWithLlmCommand
            {
                UserId = userId,
                TransactionIds = transactionIds.ToList(),
                MaxBatchSize = maxBatchSize
            };

            var result = await _mediator.Send(command, cancellationToken);

            if (result.Success)
            {
                return Ok(new
                {
                    Message = result.Message,
                    TotalTransactions = result.TotalTransactions,
                    ProcessedTransactions = result.ProcessedTransactions,
                    CandidatesCreated = result.CandidatesCreated,
                    Success = true
                });
            }
            else
            {
                return StatusCode(500, new
                {
                    Message = result.Message,
                    TotalTransactions = result.TotalTransactions,
                    ProcessedTransactions = result.ProcessedTransactions,
                    CandidatesCreated = result.CandidatesCreated,
                    Errors = result.Errors,
                    Success = false
                });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error during bulk LLM categorization");
        }
    }

    /// <summary>
    /// Gets suggestions for transactions without applying them
    /// Lightning-fast lookup of pre-processed results (Phase 2 feature)
    /// </summary>
    [HttpGet("suggestions")]
    public async Task<IActionResult> GetSuggestions(
        [FromQuery] int[] transactionIds,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (transactionIds == null || !transactionIds.Any())
        {
            return BadRequest("Transaction IDs are required");
        }

        try
        {
            // Get transactions for the user
            var transactions = await _transactionRepository.GetTransactionsByIdsAsync(transactionIds, userId, cancellationToken);
            var transactionsList = transactions.ToList();

            if (!transactionsList.Any())
            {
                return NotFound("No transactions found for the provided IDs");
            }

            // Process through pipeline but don't apply results
            var result = await _categorizationPipeline.ProcessAsync(transactionsList, cancellationToken);

            var suggestions = result.CategorizedTransactions.Select(ct => new
            {
                TransactionId = ct.Transaction.Id,
                SuggestedCategoryId = ct.CategoryId,
                SuggestedCategoryName = ct.CategoryName,
                Confidence = ct.ConfidenceScore,
                ProcessedBy = ct.ProcessedBy,
                Reason = ct.Reason,
                Metadata = ct.Metadata
            });

            return Ok(new
            {
                TotalTransactions = transactionsList.Count,
                SuggestionsFound = result.CategorizedTransactions.Count,
                ProcessingTimeMs = result.Metrics.ProcessingTime.TotalMilliseconds,
                Suggestions = suggestions
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error getting suggestions");
        }
    }

    /// <summary>
    /// Processes transactions through the full pipeline and returns suggestions as candidates
    /// Used for manual batch categorization - provides Rules, ML, and LLM suggestions
    /// </summary>
    [HttpPost("process-for-candidates")]
    public async Task<IActionResult> ProcessForCandidates(
        [FromBody] ProcessForCandidatesRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (request.TransactionIds == null || !request.TransactionIds.Any())
        {
            return BadRequest("Transaction IDs are required");
        }

        if (request.TransactionIds.Count > 100)
        {
            return BadRequest("Maximum 100 transactions can be processed at once");
        }

        try
        {
            // Get transactions for the user
            var transactions = await _transactionRepository.GetTransactionsByIdsAsync(
                request.TransactionIds, userId, cancellationToken);
            var transactionsList = transactions.ToList();

            if (!transactionsList.Any())
            {
                return NotFound("No transactions found for the provided IDs");
            }

            // Process through categorization pipeline
            var result = await _categorizationPipeline.ProcessAsync(transactionsList, cancellationToken);

            // Convert categorized transactions to candidates format for frontend compatibility
            var candidatesList = new List<object>();
            
            // Add auto-applied transactions as high-confidence candidates
            foreach (var categorized in result.AutoAppliedTransactions)
            {
                candidatesList.Add(new
                {
                    TransactionId = categorized.Transaction.Id,
                    CategoryId = categorized.CategoryId,
                    CategoryName = categorized.CategoryName,
                    Confidence = categorized.ConfidenceScore,
                    Reasoning = categorized.Reason,
                    CategorizationMethod = categorized.ProcessedBy,
                    RequiresReview = false // High confidence, could be auto-applied
                });
            }

            // Add regular candidates
            foreach (var candidate in result.Candidates)
            {
                candidatesList.Add(new
                {
                    TransactionId = candidate.TransactionId,
                    CategoryId = candidate.CategoryId,
                    CategoryName = candidate.Category?.Name ?? "Unknown",
                    Confidence = candidate.ConfidenceScore,
                    Reasoning = candidate.Reasoning,
                    CategorizationMethod = candidate.CategorizationMethod,
                    RequiresReview = true
                });
            }

            // Group by transaction for frontend compatibility with existing LLM response format
            var categorizations = request.TransactionIds.Select(transactionId =>
            {
                var suggestions = candidatesList
                    .Where(c => (int)c.GetType().GetProperty("TransactionId")!.GetValue(c)! == transactionId)
                    .Select(c => new
                    {
                        categoryId = (int)c.GetType().GetProperty("CategoryId")!.GetValue(c)!,
                        categoryName = (string)c.GetType().GetProperty("CategoryName")!.GetValue(c)!,
                        confidence = (decimal)c.GetType().GetProperty("Confidence")!.GetValue(c)!,
                        reasoning = (string)(c.GetType().GetProperty("Reasoning")!.GetValue(c) ?? ""),
                        matchingRules = new int[0], // Not implemented yet
                        categorization_method = (string)(c.GetType().GetProperty("CategorizationMethod")!.GetValue(c) ?? "Unknown")
                    }).ToList();

                return new
                {
                    transactionId,
                    suggestions,
                    requiresReview = suggestions.Any()
                };
            }).ToList();

            var processedCount = categorizations.Count(c => c.suggestions.Any());

            return Ok(new
            {
                success = true,
                categorizations,
                errors = new string[0],
                summary = new
                {
                    totalProcessed = processedCount,
                    highConfidence = categorizations.Count(c => c.suggestions.Any(s => s.confidence >= 0.8m)),
                    mediumConfidence = categorizations.Count(c => c.suggestions.Any(s => s.confidence >= 0.5m && s.confidence < 0.8m)),
                    lowConfidence = categorizations.Count(c => c.suggestions.Any(s => s.confidence < 0.5m)),
                    averageConfidence = processedCount > 0 ? 
                        categorizations.Where(c => c.suggestions.Any()).Average(c => (double)c.suggestions.First().confidence) : 0.0,
                    newRulesGenerated = 0,
                    processingTimeMs = result.Metrics.ProcessingTime.TotalMilliseconds
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error processing transactions for candidates");
        }
    }

    /// <summary>
    /// Gets pipeline performance metrics for the current user
    /// </summary>
    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        try
        {
            // This would come from a metrics tracking service in a full implementation
            // For now, return sample metrics structure
            var metrics = new
            {
                Today = new
                {
                    TransactionsProcessed = 0,
                    ProcessedByRules = 0,
                    ProcessedByML = 0,
                    ProcessedByLLM = 0,
                    CostSavings = 0.0m,
                    AverageProcessingTime = 0.0
                },
                ThisWeek = new
                {
                    TransactionsProcessed = 0,
                    ProcessedByRules = 0,
                    ProcessedByML = 0,
                    ProcessedByLLM = 0,
                    CostSavings = 0.0m,
                    AverageProcessingTime = 0.0
                },
                TopCategories = new object[0],
                PerformanceTrends = new object[0]
            };

            await Task.CompletedTask; // Placeholder for actual metrics service
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error getting metrics");
        }
    }

    // ===== CANDIDATE MANAGEMENT ENDPOINTS =====

    /// <summary>
    /// Gets AI suggestions for a single transaction (for frontend compatibility)
    /// </summary>
    [HttpGet("transaction/{transactionId}/suggestions")]
    public async Task<IActionResult> GetTransactionSuggestions(
        int transactionId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        try
        {
            // Verify transaction belongs to the user
            var transaction = await _transactionRepository.GetByIdAsync(transactionId, userId);
            if (transaction == null)
            {
                return NotFound("Transaction not found or access denied");
            }

            // Get pending candidates for this transaction
            var candidates = await _candidatesRepository.GetPendingCandidatesForTransactionAsync(
                transactionId, cancellationToken);

            // Convert to AI suggestions format for frontend compatibility
            var suggestions = await _candidatesService.ConvertCandidatesToAiSuggestionsAsync(
                candidates, cancellationToken);

            return Ok(new
            {
                TransactionId = transactionId,
                Suggestions = suggestions,
                Count = suggestions.Count()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error getting transaction suggestions");
        }
    }

    /// <summary>
    /// Gets pending categorization candidates for specific transactions
    /// </summary>
    [HttpGet("candidates")]
    public async Task<IActionResult> GetCandidates(
        [FromQuery] int[] transactionIds,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (transactionIds == null || !transactionIds.Any())
        {
            return BadRequest("Transaction IDs are required");
        }

        try
        {
            // Verify transactions belong to the user
            var transactions = await _transactionRepository.GetTransactionsByIdsAsync(transactionIds, userId, cancellationToken);
            var validTransactionIds = transactions.Select(t => t.Id).ToArray();

            if (!validTransactionIds.Any())
            {
                return NotFound("No transactions found for the provided IDs");
            }

            // Get candidates grouped by transaction
            var candidatesGrouped = await _candidatesRepository.GetCandidatesGroupedByTransactionAsync(
                validTransactionIds, cancellationToken);

            // Convert to AI suggestions format for frontend compatibility
            var suggestions = new Dictionary<int, object>();
            foreach (var group in candidatesGrouped)
            {
                var transactionSuggestions = await _candidatesService.ConvertCandidatesToAiSuggestionsAsync(
                    group.Value, cancellationToken);
                suggestions[group.Key] = transactionSuggestions;
            }

            return Ok(new
            {
                TransactionIds = validTransactionIds,
                CandidatesFound = candidatesGrouped.Sum(g => g.Value.Count),
                Suggestions = suggestions
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error getting candidates");
        }
    }

    /// <summary>
    /// Gets all pending candidates for the current user with pagination
    /// </summary>
    [HttpGet("candidates/pending")]
    public async Task<IActionResult> GetPendingCandidates(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (take > 100)
        {
            return BadRequest("Maximum 100 candidates can be requested at once");
        }

        try
        {
            var candidates = await _candidatesRepository.GetPendingCandidatesForUserAsync(userId, take + skip, cancellationToken);
            var candidatesList = candidates.Skip(skip).Take(take).ToList();

            var suggestions = await _candidatesService.ConvertCandidatesToAiSuggestionsAsync(
                candidatesList, cancellationToken);

            return Ok(new
            {
                Candidates = suggestions,
                Count = candidatesList.Count,
                Skip = skip,
                Take = take
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error getting pending candidates");
        }
    }

    /// <summary>
    /// Applies a categorization candidate to its transaction
    /// </summary>
    [HttpPost("candidates/{candidateId}/apply")]
    public async Task<IActionResult> ApplyCandidate(
        int candidateId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        try
        {
            // Verify candidate belongs to user's transaction
            var candidate = await _candidatesRepository.GetByIdAsync(candidateId, cancellationToken);
            if (candidate?.Transaction?.Account?.UserId != userId)
            {
                return NotFound("Candidate not found or access denied");
            }

            var success = await _candidatesService.ApplyCandidateAsync(
                candidateId, $"User-{userId}", cancellationToken);

            if (success)
            {
                return Ok(new { Message = "Candidate applied successfully", CandidateId = candidateId });
            }
            else
            {
                return BadRequest("Failed to apply candidate");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error applying candidate");
        }
    }

    /// <summary>
    /// Rejects a categorization candidate
    /// </summary>
    [HttpPost("candidates/{candidateId}/reject")]
    public async Task<IActionResult> RejectCandidate(
        int candidateId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        try
        {
            // Verify candidate belongs to user's transaction
            var candidate = await _candidatesRepository.GetByIdAsync(candidateId, cancellationToken);
            if (candidate?.Transaction?.Account?.UserId != userId)
            {
                return NotFound("Candidate not found or access denied");
            }

            var success = await _candidatesService.RejectCandidateAsync(
                candidateId, $"User-{userId}", cancellationToken);

            if (success)
            {
                return Ok(new { Message = "Candidate rejected successfully", CandidateId = candidateId });
            }
            else
            {
                return BadRequest("Failed to reject candidate");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error rejecting candidate");
        }
    }

    /// <summary>
    /// Applies multiple candidates in a batch operation
    /// </summary>
    [HttpPost("candidates/apply-batch")]
    public async Task<IActionResult> ApplyCandidatesBatch(
        [FromBody] int[] candidateIds,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (candidateIds == null || !candidateIds.Any())
        {
            return BadRequest("Candidate IDs are required");
        }

        if (candidateIds.Length > 50)
        {
            return BadRequest("Maximum 50 candidates can be applied at once");
        }

        try
        {
            // Verify all candidates belong to user's transactions
            var candidates = new List<CategorizationCandidate>();
            foreach (var candidateId in candidateIds)
            {
                var candidate = await _candidatesRepository.GetByIdAsync(candidateId, cancellationToken);
                if (candidate?.Transaction?.Account?.UserId == userId)
                {
                    candidates.Add(candidate);
                }
            }

            if (candidates.Count != candidateIds.Length)
            {
                return BadRequest("Some candidates not found or access denied");
            }

            var result = await _candidatesService.ApplyCandidatesBatchAsync(
                candidateIds, $"User-{userId}", userId, cancellationToken);

            return Ok(new
            {
                Message = "Batch operation completed",
                SuccessfulCount = result.SuccessfulCount,
                FailedCount = result.FailedCount,
                Errors = result.Errors,
                IsSuccess = result.IsSuccess
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error applying candidates batch");
        }
    }

    /// <summary>
    /// Rejects multiple candidates in a batch operation
    /// </summary>
    [HttpPost("candidates/reject-batch")]
    public async Task<IActionResult> RejectCandidatesBatch(
        [FromBody] int[] candidateIds,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (candidateIds == null || !candidateIds.Any())
        {
            return BadRequest("Candidate IDs are required");
        }

        if (candidateIds.Length > 50)
        {
            return BadRequest("Maximum 50 candidates can be rejected at once");
        }

        try
        {
            // Verify all candidates belong to user's transactions
            var candidates = new List<CategorizationCandidate>();
            foreach (var candidateId in candidateIds)
            {
                var candidate = await _candidatesRepository.GetByIdAsync(candidateId, cancellationToken);
                if (candidate?.Transaction?.Account?.UserId == userId)
                {
                    candidates.Add(candidate);
                }
            }

            if (candidates.Count != candidateIds.Length)
            {
                return BadRequest("Some candidates not found or access denied");
            }

            var result = await _candidatesService.RejectCandidatesBatchAsync(
                candidateIds, $"User-{userId}", cancellationToken);

            return Ok(new
            {
                Message = "Batch operation completed",
                SuccessfulCount = result.SuccessfulCount,
                FailedCount = result.FailedCount,
                Errors = result.Errors,
                IsSuccess = result.IsSuccess
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error rejecting candidates batch");
        }
    }

    /// <summary>
    /// Gets candidate statistics for the current user
    /// </summary>
    [HttpGet("candidates/stats")]
    public async Task<IActionResult> GetCandidateStats(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        try
        {
            var stats = await _candidatesService.GetCandidateStatsAsync(userId, cancellationToken);

            return Ok(new
            {
                PendingCount = stats.TotalPending,
                AppliedCount = stats.TotalApplied,
                RejectedCount = stats.TotalRejected,
                TotalCount = stats.TotalPending + stats.TotalApplied + stats.TotalRejected,
                AverageConfidence = stats.AverageConfidence,
                MethodBreakdown = stats.ByMethod,
                StatusBreakdown = stats.ByStatus
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error getting candidate stats");
        }
    }

    /// <summary>
    /// Gets categorization candidates for transactions matching the same query parameters as transaction list
    /// This enables efficient batch loading of candidates for the categorization screen
    /// </summary>
    [HttpGet("candidates/for-transaction-query")]
    public async Task<IActionResult> GetCandidatesForTransactionQuery(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] int? accountId = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] decimal? minAmount = null,
        [FromQuery] decimal? maxAmount = null,
        [FromQuery] TransactionStatus? status = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isReviewed = null,
        [FromQuery] bool? isExcluded = null,
        [FromQuery] bool? needsCategorization = null,
        [FromQuery] bool? includeTransfers = null,
        [FromQuery] bool? onlyTransfers = null,
        [FromQuery] Guid? transferId = null,
        [FromQuery] string? transactionType = null,
        [FromQuery] string sortBy = "TransactionDate",
        [FromQuery] string sortDirection = "desc",
        [FromQuery] bool onlyWithCandidates = true,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        try
        {
            // Build query parameters using the same structure as transactions endpoint
            var queryParameters = new TransactionQueryParameters
            {
                UserId = userId,
                Page = page,
                PageSize = Math.Min(pageSize, 100), // Safety limit
                AccountId = accountId,
                CategoryId = categoryId,
                StartDate = startDate?.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(startDate.Value, DateTimeKind.Utc) : startDate,
                EndDate = endDate?.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : endDate,
                MinAmount = minAmount,
                MaxAmount = maxAmount,
                Status = status,
                SearchTerm = searchTerm,
                IsReviewed = isReviewed,
                IsExcluded = isExcluded,
                NeedsCategorization = needsCategorization,
                IncludeTransfers = includeTransfers,
                OnlyTransfers = onlyTransfers,
                TransferId = transferId,
                TransactionType = transactionType,
                SortBy = sortBy,
                SortDirection = sortDirection
            };

            // Get transaction IDs that match the query
            var transactionIds = await _transactionQueryService.GetTransactionIdsAsync(queryParameters);

            if (!transactionIds.Any())
            {
                return Ok(new
                {
                    TransactionIds = transactionIds,
                    CandidatesFound = 0,
                    Suggestions = new Dictionary<int, object>()
                });
            }

            // Get candidates grouped by transaction
            var candidatesGrouped = await _candidatesRepository.GetCandidatesGroupedByTransactionAsync(
                transactionIds.ToArray(), cancellationToken);

            // If only returning transactions with candidates, filter the transaction IDs
            if (onlyWithCandidates)
            {
                transactionIds = transactionIds.Where(id => candidatesGrouped.ContainsKey(id)).ToList();
            }

            // Convert to AI suggestions format for frontend compatibility
            var suggestions = new Dictionary<int, object>();
            foreach (var transactionId in transactionIds)
            {
                if (candidatesGrouped.TryGetValue(transactionId, out var candidates))
                {
                    var transactionSuggestions = await _candidatesService.ConvertCandidatesToAiSuggestionsAsync(
                        candidates, cancellationToken);
                    suggestions[transactionId] = transactionSuggestions;
                }
                else if (!onlyWithCandidates)
                {
                    suggestions[transactionId] = new List<object>();
                }
            }

            return Ok(new
            {
                TransactionIds = transactionIds,
                CandidatesFound = candidatesGrouped.Sum(g => g.Value.Count),
                Suggestions = suggestions,
                Page = page,
                PageSize = pageSize,
                TotalTransactions = transactionIds.Count
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error getting candidates for transaction query");
        }
    }

    // ===== RULE AUTO-CATEGORIZATION ENDPOINTS =====

    /// <summary>
    /// Previews rule-based auto-categorization for filtered transactions
    /// Shows what would be categorized without actually applying the rules
    /// </summary>
    [HttpPost("auto-categorize/rules/preview")]
    public async Task<IActionResult> PreviewRuleAutoCategorization(
        [FromBody] RuleAutoCategorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        try
        {
            var filterCriteria = MapToTransactionFilterCriteria(request, userId);
            var result = await _ruleAutoCategorizationService.PreviewRuleApplicationAsync(
                filterCriteria, userId, cancellationToken);

            return Ok(new
            {
                success = true,
                isPreview = result.IsPreview,
                summary = result.Summary,
                totalExamined = result.TotalTransactionsExamined,
                transactionsSkipped = result.TransactionsSkipped,
                transactionsMatched = result.TransactionsMatched,
                transactionsUnmatched = result.TransactionsUnmatched,
                ruleMatches = result.RuleMatches,
                processingTimeMs = result.ProcessingTime.TotalMilliseconds,
                errors = result.Errors
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error previewing rule auto-categorization");
        }
    }

    /// <summary>
    /// Applies rule-based auto-categorization to filtered transactions
    /// Creates rule candidates for transactions that match rules
    /// </summary>
    [HttpPost("auto-categorize/rules/apply")]
    public async Task<IActionResult> ApplyRuleAutoCategorization(
        [FromBody] RuleAutoCategorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        try
        {
            var filterCriteria = MapToTransactionFilterCriteria(request, userId);
            var result = await _ruleAutoCategorizationService.ApplyRulesToFilteredTransactionsAsync(
                filterCriteria, userId, cancellationToken);

            return Ok(new
            {
                success = true,
                isPreview = result.IsPreview,
                summary = result.Summary,
                totalExamined = result.TotalTransactionsExamined,
                transactionsSkipped = result.TransactionsSkipped,
                transactionsMatched = result.TransactionsMatched,
                transactionsUnmatched = result.TransactionsUnmatched,
                ruleMatches = result.RuleMatches,
                processedTransactionIds = result.ProcessedTransactionIds,
                processingTimeMs = result.ProcessingTime.TotalMilliseconds,
                errors = result.Errors
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error applying rule auto-categorization");
        }
    }

    /// <summary>
    /// Applies selected rule matches to transactions (selective application)
    /// Allows users to choose which rule suggestions to apply
    /// </summary>
    [HttpPost("auto-categorize/rules/apply-selected")]
    public async Task<IActionResult> ApplySelectedRuleMatches(
        [FromBody] ApplySelectedRuleMatchesRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();

        if (request.SelectedMatches == null || !request.SelectedMatches.Any())
        {
            return BadRequest("Selected rule matches are required");
        }

        if (request.SelectedMatches.Count > 100)
        {
            return BadRequest("Maximum 100 rule matches can be applied at once");
        }

        try
        {
            var result = await _ruleAutoCategorizationService.ApplySelectedRuleMatchesAsync(
                request.SelectedMatches, userId, cancellationToken);

            return Ok(new
            {
                success = result.IsSuccess,
                isPreview = result.IsPreview,  
                summary = result.Summary,
                totalExamined = result.TotalTransactionsExamined,
                transactionsMatched = result.TransactionsMatched,
                transactionsUnmatched = result.TransactionsUnmatched,
                processedTransactionIds = result.ProcessedTransactionIds,
                processingTimeMs = result.ProcessingTime.TotalMilliseconds,
                errors = result.Errors
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error applying selected rule matches");
        }
    }

    /// <summary>
    /// Maps the API request to the internal filter criteria format
    /// </summary>
    private TransactionFilterCriteria MapToTransactionFilterCriteria(RuleAutoCategorizationRequest request, Guid userId)
    {
        return new TransactionFilterCriteria
        {
            AccountIds = request.AccountIds ?? new List<int>(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            MinAmount = request.MinAmount,
            MaxAmount = request.MaxAmount,
            TransactionType = request.TransactionType,
            SearchText = request.SearchText,
            OnlyUnreviewed = request.OnlyUnreviewed ?? true, // Default to unreviewed for categorization
            ExcludeTransfers = request.ExcludeTransfers ?? true // Default to exclude transfers
        };
    }
}

/// <summary>
/// Request for processing transactions through the full pipeline to get candidates
/// </summary>
public class ProcessForCandidatesRequest
{
    public List<int> TransactionIds { get; set; } = new();
    public decimal? ConfidenceThreshold { get; set; }
    public int? MaxBatchSize { get; set; }
}

/// <summary>
/// Request for rule-based auto-categorization with transaction filtering
/// </summary>
public class RuleAutoCategorizationRequest
{
    /// <summary>
    /// Account IDs to filter by (empty = all accounts)
    /// </summary>
    public List<int>? AccountIds { get; set; }

    /// <summary>
    /// Start date for transaction filter
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date for transaction filter  
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Minimum transaction amount filter
    /// </summary>
    public decimal? MinAmount { get; set; }

    /// <summary>
    /// Maximum transaction amount filter
    /// </summary>
    public decimal? MaxAmount { get; set; }

    /// <summary>
    /// Transaction type filter ("Income" or "Expense")
    /// </summary>
    public string? TransactionType { get; set; }

    /// <summary>
    /// Search text filter for transaction descriptions
    /// </summary>
    public string? SearchText { get; set; }

    /// <summary>
    /// Only include unreviewed transactions (default: true)
    /// </summary>
    public bool? OnlyUnreviewed { get; set; }

    /// <summary>
    /// Exclude transfer transactions (default: true)
    /// </summary>
    public bool? ExcludeTransfers { get; set; }
}

/// <summary>
/// Request for applying selected rule matches to transactions
/// </summary>
public class ApplySelectedRuleMatchesRequest
{
    /// <summary>
    /// Rule matches selected by user for application
    /// </summary>
    public List<RuleMatchDetail> SelectedMatches { get; set; } = new();
}