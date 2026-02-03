using MediatR;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Transactions.Commands;

/// <summary>
/// Command to normalize all existing transaction amounts based on their type
/// Ensures expenses are negative and income is positive for data consistency
/// </summary>
public record BulkNormalizeTransactionAmountsCommand : IRequest<BulkNormalizeTransactionAmountsResult>;

public class BulkNormalizeTransactionAmountsResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TotalTransactions { get; set; }
    public int NormalizedTransactions { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class BulkNormalizeTransactionAmountsCommandHandler : IRequestHandler<BulkNormalizeTransactionAmountsCommand, BulkNormalizeTransactionAmountsResult>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IApplicationLogger<BulkNormalizeTransactionAmountsCommandHandler> _logger;

    public BulkNormalizeTransactionAmountsCommandHandler(
        ITransactionRepository transactionRepository,
        IApplicationLogger<BulkNormalizeTransactionAmountsCommandHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    public async Task<BulkNormalizeTransactionAmountsResult> Handle(BulkNormalizeTransactionAmountsCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting bulk normalization of transaction amounts");

        var result = new BulkNormalizeTransactionAmountsResult
        {
            IsSuccess = true,
            Message = "Bulk normalization completed successfully"
        };

        try
        {
            // Get all transactions that need normalization
            var allTransactions = await _transactionRepository.GetAllTransactionsForNormalizationAsync();
            result.TotalTransactions = allTransactions.Count();

            _logger.LogInformation("Found {TotalCount} transactions to process", result.TotalTransactions);

            int normalizedCount = 0;
            int errorCount = 0;

            foreach (var transaction in allTransactions)
            {
                try
                {
                    bool needsNormalization = false;

                    // Check if transaction needs normalization based on type vs amount sign
                    if (transaction.Type == TransactionType.Expense && transaction.Amount > 0)
                    {
                        // Expense with positive amount - needs to be negative
                        transaction.Amount = -Math.Abs(transaction.Amount);
                        needsNormalization = true;
                        _logger.LogDebug("Normalizing expense transaction {Id}: {Amount} -> {NewAmount}", 
                            transaction.Id, Math.Abs(transaction.Amount), transaction.Amount);
                    }
                    else if (transaction.Type == TransactionType.Income && transaction.Amount < 0)
                    {
                        // Income with negative amount - needs to be positive
                        transaction.Amount = Math.Abs(transaction.Amount);
                        needsNormalization = true;
                        _logger.LogDebug("Normalizing income transaction {Id}: {Amount} -> {NewAmount}", 
                            transaction.Id, -transaction.Amount, transaction.Amount);
                    }
                    else if (transaction.Type == TransactionType.TransferComponent)
                    {
                        // Transfer components keep their original amounts (can be positive or negative based on direction)
                        // No normalization needed
                        _logger.LogDebug("Skipping transfer component transaction {Id}", transaction.Id);
                    }

                    if (needsNormalization)
                    {
                        transaction.UpdatedAt = DateTime.UtcNow;
                        await _transactionRepository.UpdateAsync(transaction);
                        normalizedCount++;

                        _logger.LogDebug("Normalized transaction {Id}: Type={Type}, Amount={Amount}", 
                            transaction.Id, transaction.Type, transaction.Amount);
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    var errorMessage = $"Failed to normalize transaction {transaction.Id}: {ex.Message}";
                    result.Errors.Add(errorMessage);
                    _logger.LogError(ex, "Error normalizing transaction {TransactionId}", transaction.Id);
                }
            }

            // Save all changes in a single batch
            if (normalizedCount > 0)
            {
                await _transactionRepository.SaveChangesAsync();
                _logger.LogInformation("Successfully normalized {NormalizedCount} transactions", normalizedCount);
            }

            result.NormalizedTransactions = normalizedCount;
            result.ErrorCount = errorCount;

            if (errorCount > 0)
            {
                result.IsSuccess = false;
                result.Message = $"Completed with {errorCount} errors. {normalizedCount} transactions normalized successfully.";
            }
            else
            {
                result.Message = $"Successfully normalized {normalizedCount} out of {result.TotalTransactions} transactions.";
            }

            _logger.LogInformation("Bulk normalization completed: {NormalizedCount} normalized, {ErrorCount} errors", 
                normalizedCount, errorCount);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute bulk transaction amount normalization");
            
            return new BulkNormalizeTransactionAmountsResult
            {
                IsSuccess = false,
                Message = $"Bulk normalization failed: {ex.Message}",
                TotalTransactions = result.TotalTransactions,
                NormalizedTransactions = result.NormalizedTransactions,
                ErrorCount = result.ErrorCount + 1,
                Errors = result.Errors.Concat(new[] { ex.Message }).ToList()
            };
        }
    }
}