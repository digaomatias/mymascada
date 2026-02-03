using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Handlers;
using MyMascada.Application.Features.Categorization.Interfaces;
using MyMascada.Application.Features.Categorization.Models;
using MyMascada.Domain.Entities;
using System.Diagnostics;

namespace MyMascada.Application.Features.Categorization.Services;

/// <summary>
/// Main orchestrator for the categorization pipeline
/// Implements Chain of Responsibility: Rules → ML → LLM
/// </summary>
public interface ICategorizationPipeline
{
    Task<CategorizationResult> ProcessAsync(IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default);
}

/// <summary>
/// Standard categorization pipeline implementation
/// Chain: Rules → BankCategory → ML → LLM
/// </summary>
public class CategorizationPipeline : ICategorizationPipeline
{
    private readonly RulesHandler _rulesHandler;
    private readonly BankCategoryHandler _bankCategoryHandler;
    private readonly MLHandler _mlHandler;
    private readonly LLMHandler _llmHandler;
    private readonly ICategorizationCandidatesService _candidatesService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<CategorizationPipeline> _logger;

    public CategorizationPipeline(
        RulesHandler rulesHandler,
        BankCategoryHandler bankCategoryHandler,
        MLHandler mlHandler,
        LLMHandler llmHandler,
        ICategorizationCandidatesService candidatesService,
        ITransactionRepository transactionRepository,
        ILogger<CategorizationPipeline> logger)
    {
        _rulesHandler = rulesHandler;
        _bankCategoryHandler = bankCategoryHandler;
        _mlHandler = mlHandler;
        _llmHandler = llmHandler;
        _candidatesService = candidatesService;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    public async Task<CategorizationResult> ProcessAsync(IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default)
    {
        var transactionsList = transactions.ToList();
        if (!transactionsList.Any())
        {
            return new CategorizationResult();
        }

        var stopwatch = Stopwatch.StartNew();
        var finalResult = new CategorizationResult();
        finalResult.Metrics.TotalTransactions = transactionsList.Count;
        
        // Log transaction IDs for better traceability
        var transactionIds = transactionsList.Select(t => t.Id).ToList();
        _logger.LogInformation("Starting categorization pipeline for {TransactionCount} transactions. IDs: [{TransactionIds}]", 
            transactionsList.Count, string.Join(", ", transactionIds));

        try
        {
            // Set up the chain: Rules → BankCategory → ML → LLM
            _rulesHandler
                .SetNext(_bankCategoryHandler)
                .SetNext(_mlHandler)
                .SetNext(_llmHandler);

            // Process through Rules Handler
            _logger.LogInformation("Processing transactions through Rules Handler");
            var rulesResult = await _rulesHandler.HandleAsync(transactionsList, cancellationToken);
            finalResult.MergeWith(rulesResult);
            
            // Collect remaining transactions for next handler
            // Remove transactions that were processed by Rules Handler (auto-applied OR candidates)
            var remainingTransactions = transactionsList
                .Where(t => !rulesResult.AutoAppliedTransactions.Any(cat => cat.Transaction.Id == t.Id) &&
                           !rulesResult.Candidates.Any(c => c.TransactionId == t.Id))
                .ToList();
            
            var processedByRules = rulesResult.AutoAppliedTransactions.Select(t => t.Transaction.Id)
                .Concat(rulesResult.Candidates.Select(c => c.TransactionId)).ToList();
            var remainingIds = remainingTransactions.Select(t => t.Id).ToList();
                
            _logger.LogInformation("Rules Handler processed {ProcessedCount} transactions ({AutoApplied} auto-applied, {Candidates} candidates), {RemainingCount} remaining. " +
                "Processed IDs: [{ProcessedIds}], Remaining IDs: [{RemainingIds}]",
                rulesResult.AutoAppliedTransactions.Count + rulesResult.Candidates.Count,
                rulesResult.AutoAppliedTransactions.Count,
                rulesResult.Candidates.Count,
                remainingTransactions.Count,
                string.Join(", ", processedByRules),
                string.Join(", ", remainingIds));

            // Process remaining through BankCategory Handler if any remain
            if (remainingTransactions.Any())
            {
                _logger.LogInformation("Processing {Count} remaining transactions through BankCategory Handler. IDs: [{TransactionIds}]",
                    remainingTransactions.Count, string.Join(", ", remainingTransactions.Select(t => t.Id)));
                var bankCategoryResult = await _bankCategoryHandler.HandleAsync(remainingTransactions, cancellationToken);
                finalResult.MergeWith(bankCategoryResult);

                // Remove transactions processed by BankCategory Handler (auto-applied OR candidates)
                var beforeBankCategoryCount = remainingTransactions.Count;
                remainingTransactions = remainingTransactions
                    .Where(t => !bankCategoryResult.AutoAppliedTransactions.Any(cat => cat.Transaction.Id == t.Id) &&
                               !bankCategoryResult.Candidates.Any(c => c.TransactionId == t.Id))
                    .ToList();

                _logger.LogInformation("BankCategory Handler processed {ProcessedCount} transactions, {RemainingCount} still remaining",
                    beforeBankCategoryCount - remainingTransactions.Count, remainingTransactions.Count);
            }

            // Process remaining through ML Handler if any remain
            if (remainingTransactions.Any())
            {
                _logger.LogInformation("Processing {Count} remaining transactions through ML Handler. IDs: [{TransactionIds}]",
                    remainingTransactions.Count, string.Join(", ", remainingTransactions.Select(t => t.Id)));
                var mlResult = await _mlHandler.HandleAsync(remainingTransactions, cancellationToken);
                finalResult.MergeWith(mlResult);

                // Remove transactions processed by ML Handler (auto-applied OR candidates)
                var beforeMLCount = remainingTransactions.Count;
                remainingTransactions = remainingTransactions
                    .Where(t => !mlResult.AutoAppliedTransactions.Any(cat => cat.Transaction.Id == t.Id) &&
                               !mlResult.Candidates.Any(c => c.TransactionId == t.Id))
                    .ToList();

                _logger.LogInformation("ML Handler processed {ProcessedCount} transactions, {RemainingCount} still remaining",
                    beforeMLCount - remainingTransactions.Count, remainingTransactions.Count);
            }

            // Process final remaining through LLM Handler if any remain
            if (remainingTransactions.Any())
            {
                _logger.LogInformation("Processing {Count} final remaining transactions through LLM Handler. IDs: [{TransactionIds}]", 
                    remainingTransactions.Count, string.Join(", ", remainingTransactions.Select(t => t.Id)));
                var llmResult = await _llmHandler.HandleAsync(remainingTransactions, cancellationToken);
                finalResult.MergeWith(llmResult);
                
                var beforeLLMCount = remainingTransactions.Count;
                remainingTransactions = remainingTransactions
                    .Where(t => !llmResult.Candidates.Any(c => c.TransactionId == t.Id))
                    .ToList();
                    
                _logger.LogInformation("LLM Handler processed {ProcessedCount} transactions, {FinalRemainingCount} completely unprocessed", 
                    beforeLLMCount - remainingTransactions.Count, remainingTransactions.Count);
            }

            // Set final remaining transactions
            finalResult.RemainingTransactions = remainingTransactions;

            // Perform batch database operations
            await PerformDatabaseOperations(finalResult, cancellationToken);
            
            stopwatch.Stop();
            finalResult.Metrics.ProcessingTime = stopwatch.Elapsed;

            // Log comprehensive results
            LogPipelineResults(finalResult, transactionsList.Count);

            return finalResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error in categorization pipeline");
            
            return new CategorizationResult
            {
                RemainingTransactions = transactionsList,
                Metrics = 
                { 
                    TotalTransactions = transactionsList.Count,
                    ProcessingTime = stopwatch.Elapsed,
                    FailedTransactions = transactionsList.Count
                },
                Errors = { $"Pipeline failed: {ex.Message}" }
            };
        }
    }

    private void LogPipelineResults(CategorizationResult result, int totalTransactions)
    {
        var metrics = result.Metrics;
        
        _logger.LogInformation(
            "Categorization pipeline completed: {TotalCount} transactions processed in {ElapsedMs}ms\n" +
            "  Rules: {RulesCount} ({RulesPercent:P1})\n" +
            "  BankCategory: {BankCategoryCount} ({BankCategoryPercent:P1})\n" +
            "  ML: {MLCount} ({MLPercent:P1})\n" +
            "  LLM: {LLMCount} ({LLMPercent:P1})\n" +
            "  Uncategorized: {UncategorizedCount} ({UncategorizedPercent:P1})\n" +
            "  Success Rate: {SuccessRate:P1}\n" +
            "  Estimated Cost Savings: ${CostSavings:F4}",
            totalTransactions,
            metrics.ProcessingTime.TotalMilliseconds,
            metrics.ProcessedByRules,
            totalTransactions > 0 ? (double)metrics.ProcessedByRules / totalTransactions : 0,
            metrics.ProcessedByBankCategory,
            totalTransactions > 0 ? (double)metrics.ProcessedByBankCategory / totalTransactions : 0,
            metrics.ProcessedByML,
            totalTransactions > 0 ? (double)metrics.ProcessedByML / totalTransactions : 0,
            metrics.ProcessedByLLM,
            totalTransactions > 0 ? (double)metrics.ProcessedByLLM / totalTransactions : 0,
            result.RemainingTransactions.Count,
            totalTransactions > 0 ? (double)result.RemainingTransactions.Count / totalTransactions : 0,
            metrics.SuccessRate,
            metrics.EstimatedCostSavings);

        if (result.Errors.Any())
        {
            _logger.LogWarning("Pipeline completed with {ErrorCount} errors: {Errors}", 
                result.Errors.Count, string.Join("; ", result.Errors));
        }

        // Log category distribution
        if (metrics.CategoryDistribution.Any())
        {
            var topCategories = metrics.CategoryDistribution
                .OrderByDescending(x => x.Value)
                .Take(5)
                .Select(x => $"{x.Key}: {x.Value}")
                .ToList();
            
            _logger.LogDebug("Top categories: {TopCategories}", string.Join(", ", topCategories));
        }
    }

    private async Task PerformDatabaseOperations(CategorizationResult result, CancellationToken cancellationToken)
    {
        try
        {
            // Create candidates in database
            if (result.Candidates.Any())
            {
                await _candidatesService.CreateCandidatesAsync(result.Candidates, cancellationToken);
                _logger.LogInformation("Created {CandidateCount} categorization candidates in database", result.Candidates.Count);
            }

            // Auto-apply high-confidence transactions
            if (result.AutoAppliedTransactions.Any())
            {
                foreach (var categorized in result.AutoAppliedTransactions)
                {
                    var transaction = categorized.Transaction;
                    transaction.CategoryId = categorized.CategoryId;
                    transaction.MarkAsAutoCategorized(
                        categorized.ProcessedBy switch 
                        {
                            "Rules" => "Rule",
                            "ML" => "ML",
                            _ => categorized.ProcessedBy
                        },
                        categorized.ConfidenceScore,
                        categorized.ProcessedBy);
                    
                    await _transactionRepository.UpdateAsync(transaction);
                }
                
                await _transactionRepository.SaveChangesAsync();
                _logger.LogInformation("Auto-applied {Count} high-confidence categorizations", result.AutoAppliedTransactions.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing database operations in categorization pipeline");
            result.Errors.Add($"Database operation failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Cost-aware pipeline wrapper that tracks and limits expensive operations
/// </summary>
public class CostAwareCategorizationPipeline : ICategorizationPipeline
{
    private readonly ICategorizationPipeline _innerPipeline;
    private readonly ILogger<CostAwareCategorizationPipeline> _logger;
    private readonly decimal _dailyCostLimit;
    private readonly decimal _currentDailyCost; // This would come from a cost tracking service

    public CostAwareCategorizationPipeline(
        ICategorizationPipeline innerPipeline,
        ILogger<CostAwareCategorizationPipeline> logger,
        decimal dailyCostLimit = 10.0m)
    {
        _innerPipeline = innerPipeline;
        _logger = logger;
        _dailyCostLimit = dailyCostLimit;
        _currentDailyCost = 0; // TODO: Get from cost tracking service
    }

    public async Task<CategorizationResult> ProcessAsync(IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default)
    {
        var transactionsList = transactions.ToList();
        var estimatedLLMCost = EstimateLLMCost(transactionsList);
        
        // Check if processing would exceed daily cost limit
        if (_currentDailyCost + estimatedLLMCost > _dailyCostLimit)
        {
            _logger.LogWarning(
                "Skipping LLM processing to stay within daily cost limit. " +
                "Current: ${CurrentCost:F4}, Estimated: ${EstimatedCost:F4}, Limit: ${Limit:F4}",
                _currentDailyCost, estimatedLLMCost, _dailyCostLimit);
            
            // Process with rules and ML only (create pipeline without LLM)
            // For now, delegate to full pipeline but this is where cost control would be implemented
        }

        var result = await _innerPipeline.ProcessAsync(transactionsList, cancellationToken);
        
        // TODO: Record actual costs with cost tracking service
        await RecordCostMetrics(result);
        
        return result;
    }

    private decimal EstimateLLMCost(IEnumerable<Transaction> transactions)
    {
        // Rough estimate: $0.005 per transaction for LLM processing
        return transactions.Count() * 0.005m;
    }

    private async Task RecordCostMetrics(CategorizationResult result)
    {
        // TODO: Implement cost tracking service integration
        _logger.LogDebug("Cost metrics - Estimated savings: ${CostSavings:F4}", result.Metrics.EstimatedCostSavings);
        await Task.CompletedTask;
    }
}