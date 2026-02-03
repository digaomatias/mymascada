using Microsoft.Extensions.Logging;
using MyMascada.Application.Features.Categorization.Interfaces;
using MyMascada.Application.Features.Categorization.Models;
using MyMascada.Domain.Entities;
using System.Diagnostics;

namespace MyMascada.Application.Features.Categorization.Handlers;

/// <summary>
/// Base class for categorization handlers implementing Chain of Responsibility pattern
/// </summary>
public abstract class CategorizationHandler : ICategorizationHandler
{
    private ICategorizationHandler? _nextHandler;
    protected readonly ILogger _logger;

    protected CategorizationHandler(ILogger logger)
    {
        _logger = logger;
    }

    public abstract string HandlerType { get; }

    public ICategorizationHandler SetNext(ICategorizationHandler handler)
    {
        _nextHandler = handler;
        return handler;
    }

    public virtual async Task<CategorizationResult> HandleAsync(IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var transactionList = transactions.ToList();
        
        _logger.LogDebug("Starting {HandlerType} processing for {TransactionCount} transactions", 
            HandlerType, transactionList.Count);

        try
        {
            // Process transactions with this handler
            var result = await ProcessTransactionsAsync(transactionList, cancellationToken);
            result.Metrics.TotalTransactions = transactionList.Count;
            result.Metrics.ProcessingTime = stopwatch.Elapsed;

            // Determine remaining transactions that weren't categorized
            var categorizedTransactionIds = result.CategorizedTransactions.Select(ct => ct.Transaction.Id).ToHashSet();
            var remainingTransactions = transactionList.Where(t => !categorizedTransactionIds.Contains(t.Id)).ToList();
            result.RemainingTransactions = remainingTransactions;

            _logger.LogInformation("{HandlerType} processed {CategorizedCount}/{TotalCount} transactions in {ElapsedMs}ms", 
                HandlerType, 
                result.CategorizedTransactions.Count, 
                transactionList.Count, 
                stopwatch.ElapsedMilliseconds);

            // Pass remaining transactions to next handler if available
            if (_nextHandler != null && remainingTransactions.Any())
            {
                _logger.LogDebug("Passing {RemainingCount} transactions to next handler: {NextHandlerType}", 
                    remainingTransactions.Count, _nextHandler.HandlerType);

                var nextResult = await _nextHandler.HandleAsync(remainingTransactions, cancellationToken);
                result = result.MergeWith(nextResult);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {HandlerType} processing", HandlerType);
            
            // Create error result and pass all transactions to next handler
            var errorResult = new CategorizationResult
            {
                RemainingTransactions = transactionList,
                Metrics = { TotalTransactions = transactionList.Count, ProcessingTime = stopwatch.Elapsed },
                Errors = { $"{HandlerType} failed: {ex.Message}" }
            };

            if (_nextHandler != null)
            {
                var nextResult = await _nextHandler.HandleAsync(transactionList, cancellationToken);
                errorResult = errorResult.MergeWith(nextResult);
            }

            return errorResult;
        }
    }

    /// <summary>
    /// Processes transactions specific to this handler
    /// Should return transactions that were successfully categorized
    /// </summary>
    protected abstract Task<CategorizationResult> ProcessTransactionsAsync(
        IEnumerable<Transaction> transactions, 
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates metrics based on handler type
    /// </summary>
    protected void UpdateMetrics(CategorizationResult result, int processedCount)
    {
        switch (HandlerType.ToLower())
        {
            case "rules":
                result.Metrics.ProcessedByRules = processedCount;
                result.Metrics.EstimatedCostSavings = processedCount * 0.005m; // Assume $0.005 saved per LLM call
                break;
            case "bankcategory":
                result.Metrics.ProcessedByBankCategory = processedCount;
                result.Metrics.EstimatedCostSavings = processedCount * 0.005m; // Assume $0.005 saved per LLM call
                break;
            case "ml":
                result.Metrics.ProcessedByML = processedCount;
                result.Metrics.EstimatedCostSavings = processedCount * 0.004m; // Assume $0.004 saved per LLM call
                break;
            case "llm":
                result.Metrics.ProcessedByLLM = processedCount;
                // No cost savings for LLM as it's the expensive option
                break;
        }
    }

    /// <summary>
    /// Creates a categorized transaction with standard metadata
    /// </summary>
    protected CategorizedTransaction CreateCategorizedTransaction(
        Transaction transaction, 
        int categoryId, 
        string categoryName, 
        decimal confidence, 
        string reason = "",
        Dictionary<string, object>? metadata = null)
    {
        return new CategorizedTransaction(transaction, categoryId, categoryName, confidence, HandlerType, reason)
        {
            Metadata = metadata ?? new Dictionary<string, object>()
        };
    }
}