using MediatR;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.DescriptionCleaning.Commands;

/// <summary>
/// Command to clean transaction descriptions using LLM for a given user.
/// Fetches transactions, skips those with existing UserDescription, extracts
/// MerchantNameHint from Notes, and processes in batches of 50.
/// </summary>
public class CleanTransactionDescriptionsCommand : IRequest<CleanTransactionDescriptionsResult>
{
    public Guid UserId { get; set; }
    public List<int> TransactionIds { get; set; } = new();
    public decimal ConfidenceThreshold { get; set; } = 0.7m;
    public int MaxBatchSize { get; set; } = 50;
}

public class CleanTransactionDescriptionsResult
{
    public int TotalTransactions { get; set; }
    public int ProcessedTransactions { get; set; }
    public int CleanedTransactions { get; set; }
    public int SkippedTransactions { get; set; }
    public List<CleanedTransactionPreview> Previews { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool Success => Errors.Count == 0;
    public string Message { get; set; } = string.Empty;
}

public class CleanedTransactionPreview
{
    public int TransactionId { get; set; }
    public string OriginalDescription { get; set; } = string.Empty;
    public string CleanedDescription { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
}

public class CleanTransactionDescriptionsCommandHandler
    : IRequestHandler<CleanTransactionDescriptionsCommand, CleanTransactionDescriptionsResult>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountAccessService _accountAccessService;
    private readonly IDescriptionCleaningService _descriptionCleaningService;
    private readonly ILogger<CleanTransactionDescriptionsCommandHandler> _logger;

    public CleanTransactionDescriptionsCommandHandler(
        ITransactionRepository transactionRepository,
        IAccountAccessService accountAccessService,
        IDescriptionCleaningService descriptionCleaningService,
        ILogger<CleanTransactionDescriptionsCommandHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _accountAccessService = accountAccessService;
        _descriptionCleaningService = descriptionCleaningService;
        _logger = logger;
    }

    public async Task<CleanTransactionDescriptionsResult> Handle(
        CleanTransactionDescriptionsCommand request,
        CancellationToken cancellationToken)
    {
        var result = new CleanTransactionDescriptionsResult
        {
            TotalTransactions = request.TransactionIds.Count
        };

        try
        {
            _logger.LogInformation(
                "Starting description cleaning for {TransactionCount} transactions for user {UserId}",
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

            // Verify the user has modify permission on all affected accounts
            var accountIds = transactionsList.Select(t => t.AccountId).Distinct();
            foreach (var accountId in accountIds)
            {
                if (!await _accountAccessService.CanModifyAccountAsync(request.UserId, accountId))
                {
                    throw new UnauthorizedAccessException(
                        "You do not have permission to modify transactions on one or more of these accounts.");
                }
            }

            // Filter out transactions that already have a UserDescription
            var eligibleTransactions = transactionsList
                .Where(t => string.IsNullOrWhiteSpace(t.UserDescription))
                .ToList();

            result.SkippedTransactions = transactionsList.Count - eligibleTransactions.Count;

            if (!eligibleTransactions.Any())
            {
                result.Message = "All transactions already have user descriptions";
                return result;
            }

            // Process in batches
            var batches = eligibleTransactions
                .Select((t, i) => new { Transaction = t, Index = i })
                .GroupBy(x => x.Index / request.MaxBatchSize)
                .Select(g => g.Select(x => x.Transaction).ToList())
                .ToList();

            _logger.LogInformation(
                "Processing {TransactionCount} eligible transactions in {BatchCount} batches (skipped {SkippedCount} with existing UserDescription)",
                eligibleTransactions.Count, batches.Count, result.SkippedTransactions);

            foreach (var batch in batches)
            {
                try
                {
                    var inputs = batch.Select(t => new DescriptionCleaningInput
                    {
                        TransactionId = t.Id,
                        OriginalDescription = t.Description,
                        MerchantNameHint = ExtractMerchantNameHint(t.Notes)
                    }).ToList();

                    var cleaningResponse = await _descriptionCleaningService.CleanDescriptionsAsync(
                        inputs, cancellationToken);

                    if (!cleaningResponse.Success)
                    {
                        _logger.LogWarning(
                            "Description cleaning service failed for batch: {Errors}",
                            string.Join(", ", cleaningResponse.Errors));
                        result.Errors.AddRange(cleaningResponse.Errors);
                        continue;
                    }

                    // Apply results that meet confidence threshold
                    foreach (var cleaned in cleaningResponse.Results)
                    {
                        var preview = new CleanedTransactionPreview
                        {
                            TransactionId = cleaned.TransactionId,
                            OriginalDescription = cleaned.OriginalDescription,
                            CleanedDescription = cleaned.Description,
                            Confidence = cleaned.Confidence,
                            Reasoning = cleaned.Reasoning
                        };
                        result.Previews.Add(preview);

                        if (cleaned.Confidence >= request.ConfidenceThreshold)
                        {
                            var transaction = batch.FirstOrDefault(t => t.Id == cleaned.TransactionId);
                            if (transaction != null)
                            {
                                transaction.UserDescription = cleaned.Description;
                                await _transactionRepository.UpdateAsync(transaction);
                                result.CleanedTransactions++;
                            }
                        }
                    }

                    result.ProcessedTransactions += batch.Count;

                    _logger.LogDebug(
                        "Processed batch of {BatchSize} transactions, cleaned {CleanedCount} descriptions",
                        batch.Count, cleaningResponse.Results.Count(r => r.Confidence >= request.ConfidenceThreshold));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing description cleaning batch of {BatchSize} transactions",
                        batch.Count);
                    result.Errors.Add($"Batch processing error: {ex.Message}");
                }
            }

            await _transactionRepository.SaveChangesAsync();

            result.Message = result.Success
                ? $"Successfully cleaned {result.CleanedTransactions} of {result.ProcessedTransactions} transaction descriptions"
                : $"Processed {result.ProcessedTransactions} transactions with {result.Errors.Count} errors";

            return result;
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error in description cleaning");
            result.Errors.Add($"Critical error: {ex.Message}");
            result.Message = "Description cleaning failed due to a critical error";
            return result;
        }
    }

    /// <summary>
    /// Extracts a merchant name hint from the transaction Notes field.
    /// Looks for patterns like "Merchant: XYZ" or "From: XYZ" in the notes.
    /// </summary>
    private static string? ExtractMerchantNameHint(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return null;

        // Look for common patterns in notes that indicate merchant name
        var lines = notes.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Merchant:", StringComparison.OrdinalIgnoreCase))
                return trimmed.Substring("Merchant:".Length).Trim();
            if (trimmed.StartsWith("From:", StringComparison.OrdinalIgnoreCase))
                return trimmed.Substring("From:".Length).Trim();
            if (trimmed.StartsWith("Payee:", StringComparison.OrdinalIgnoreCase))
                return trimmed.Substring("Payee:".Length).Trim();
        }

        return null;
    }
}
