using MediatR;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Models;
using MyMascada.Application.Features.Categorization.Services;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Transactions.Commands;

/// <summary>
/// Command for bulk categorization using only the LLM, bypassing the pipeline
/// Used for user-initiated bulk categorization actions
/// </summary>
public class BulkCategorizeWithLlmCommand : IRequest<BulkLlmCategorizationResult>
{
    public Guid UserId { get; set; }
    public List<int> TransactionIds { get; set; } = new();
    public int MaxBatchSize { get; set; } = 50;
}

/// <summary>
/// Result of bulk LLM categorization
/// </summary>
public class BulkLlmCategorizationResult
{
    public int TotalTransactions { get; set; }
    public int ProcessedTransactions { get; set; }
    public int CandidatesCreated { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool Success => Errors.Count == 0;
    public string Message { get; set; } = string.Empty;
}

public class BulkCategorizeWithLlmCommandHandler : IRequestHandler<BulkCategorizeWithLlmCommand, BulkLlmCategorizationResult>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ISharedCategorizationService _sharedCategorizationService;
    private readonly ICategorizationCandidatesService _candidatesService;
    private readonly ILogger<BulkCategorizeWithLlmCommandHandler> _logger;

    public BulkCategorizeWithLlmCommandHandler(
        ITransactionRepository transactionRepository,
        ISharedCategorizationService sharedCategorizationService,
        ICategorizationCandidatesService candidatesService,
        ILogger<BulkCategorizeWithLlmCommandHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _sharedCategorizationService = sharedCategorizationService;
        _candidatesService = candidatesService;
        _logger = logger;
    }

    public async Task<BulkLlmCategorizationResult> Handle(BulkCategorizeWithLlmCommand request, CancellationToken cancellationToken)
    {
        var result = new BulkLlmCategorizationResult
        {
            TotalTransactions = request.TransactionIds.Count
        };

        try
        {
            _logger.LogInformation(
                "Starting bulk LLM categorization for {TransactionCount} transactions for user {UserId}",
                request.TransactionIds.Count, request.UserId);

            // Fetch transactions
            var transactions = await _transactionRepository.GetTransactionsByIdsAsync(
                request.TransactionIds, request.UserId, cancellationToken);
            
            var transactionsList = transactions.ToList();
            if (!transactionsList.Any())
            {
                result.Message = "No valid transactions found for the provided IDs";
                return result;
            }

            // Process in batches to respect limits
            var batches = transactionsList
                .Select((t, i) => new { Transaction = t, Index = i })
                .GroupBy(x => x.Index / request.MaxBatchSize)
                .Select(g => g.Select(x => x.Transaction).ToList())
                .ToList();

            _logger.LogInformation(
                "Processing {TransactionCount} transactions in {BatchCount} batches of up to {BatchSize} each",
                transactionsList.Count, batches.Count, request.MaxBatchSize);

            var allCandidates = new List<MyMascada.Domain.Entities.CategorizationCandidate>();

            foreach (var batch in batches)
            {
                try
                {
                    // Get LLM suggestions for this batch
                    var llmResponse = await _sharedCategorizationService.GetCategorizationSuggestionsAsync(
                        batch, request.UserId, cancellationToken);

                    if (!llmResponse.Success)
                    {
                        _logger.LogWarning(
                            "LLM service failed for batch: {Errors}",
                            string.Join(", ", llmResponse.Errors));
                        result.Errors.AddRange(llmResponse.Errors);
                        continue;
                    }

                    // Convert to candidates
                    var candidates = _sharedCategorizationService.ConvertToCategorizationCandidates(
                        llmResponse, $"BulkLLM-{request.UserId}");
                    
                    allCandidates.AddRange(candidates);
                    result.ProcessedTransactions += batch.Count;

                    _logger.LogDebug(
                        "Processed batch of {BatchSize} transactions, generated {CandidateCount} candidates",
                        batch.Count, candidates.Count());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing batch of {BatchSize} transactions", batch.Count);
                    result.Errors.Add($"Batch processing error: {ex.Message}");
                }
            }

            // Create all candidates
            if (allCandidates.Any())
            {
                var createdCandidates = await _candidatesService.CreateCandidatesAsync(
                    allCandidates, cancellationToken);
                
                result.CandidatesCreated = createdCandidates.Count();
                
                _logger.LogInformation(
                    "Created {CandidateCount} LLM candidates for user {UserId}",
                    result.CandidatesCreated, request.UserId);
            }

            result.Message = result.Success
                ? $"Successfully processed {result.ProcessedTransactions} transactions and created {result.CandidatesCreated} candidates"
                : $"Processed {result.ProcessedTransactions} transactions with {result.Errors.Count} errors";

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error in bulk LLM categorization");
            result.Errors.Add($"Critical error: {ex.Message}");
            result.Message = "Bulk categorization failed due to a critical error";
            return result;
        }
    }
}
