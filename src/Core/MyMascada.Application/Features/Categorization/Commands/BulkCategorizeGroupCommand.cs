using MediatR;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Services;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Categorization.Commands;

/// <summary>
/// Categorizes every transaction in a group in one shot AND records
/// CategorizationHistory entries so the ML handler learns from the
/// pattern and auto-applies it to future transactions.
/// </summary>
public class BulkCategorizeGroupCommand : IRequest<BulkCategorizeGroupResult>
{
    public Guid UserId { get; set; }
    public List<int> TransactionIds { get; set; } = new();
    public int CategoryId { get; set; }

    /// <summary>
    /// Optional — the normalized description of the group being categorized.
    /// When supplied, a single history entry is recorded for the group key
    /// (instead of one per transaction) which is more efficient and avoids
    /// inflating the MatchCount beyond what the user actually confirmed.
    /// </summary>
    public string? NormalizedDescription { get; set; }
}

public class BulkCategorizeGroupResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TransactionsUpdated { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class BulkCategorizeGroupCommandHandler
    : IRequestHandler<BulkCategorizeGroupCommand, BulkCategorizeGroupResult>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IAccountAccessService _accountAccessService;
    private readonly ICategorizationHistoryService _historyService;
    private readonly ILogger<BulkCategorizeGroupCommandHandler> _logger;

    public BulkCategorizeGroupCommandHandler(
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        IAccountAccessService accountAccessService,
        ICategorizationHistoryService historyService,
        ILogger<BulkCategorizeGroupCommandHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _categoryRepository = categoryRepository;
        _accountAccessService = accountAccessService;
        _historyService = historyService;
        _logger = logger;
    }

    public async Task<BulkCategorizeGroupResult> Handle(
        BulkCategorizeGroupCommand request,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (request.TransactionIds == null || request.TransactionIds.Count == 0)
        {
            return new BulkCategorizeGroupResult
            {
                Success = false,
                Message = "No transaction IDs provided",
                Errors = new List<string> { "No transaction IDs provided" }
            };
        }

        if (!await _categoryRepository.ExistsAsync(request.CategoryId, request.UserId))
        {
            return new BulkCategorizeGroupResult
            {
                Success = false,
                Message = "Category not found or access denied",
                Errors = new List<string> { "Category not found or access denied" }
            };
        }

        var transactions = (await _transactionRepository.GetTransactionsByIdsAsync(
            request.TransactionIds,
            request.UserId,
            cancellationToken)).ToList();

        var foundIds = transactions.Select(t => t.Id).ToHashSet();
        var missingIds = request.TransactionIds.Where(id => !foundIds.Contains(id)).ToList();
        if (missingIds.Count > 0)
        {
            errors.Add($"Transactions not found or access denied: {string.Join(", ", missingIds)}");
        }

        // Verify user can modify each account involved
        var accountIds = transactions.Select(t => t.AccountId).Distinct().ToList();
        foreach (var accountId in accountIds)
        {
            if (!await _accountAccessService.CanModifyAccountAsync(request.UserId, accountId))
            {
                throw new UnauthorizedAccessException(
                    "You do not have permission to update transactions on one or more of these accounts.");
            }
        }

        var updatedCount = 0;
        var changedTransactions = new List<Transaction>();
        var now = DateTime.UtcNow;

        foreach (var transaction in transactions)
        {
            if (transaction.TransferId.HasValue)
            {
                errors.Add($"Transaction {transaction.Id} is part of a transfer and cannot be categorized.");
                continue;
            }

            var categoryChanged = transaction.CategoryId != request.CategoryId;

            transaction.CategoryId = request.CategoryId;
            transaction.UpdatedAt = now;
            transaction.UpdatedBy = request.UserId.ToString();
            // Quick-categorize is an intentional user action — mark reviewed
            // so the transactions do not reappear in the review queue.
            transaction.IsReviewed = true;

            updatedCount++;

            if (categoryChanged)
                changedTransactions.Add(transaction);
        }

        await _transactionRepository.SaveChangesAsync();

        // Record categorization history — best-effort (the category update is
        // already committed above). The ML handler reads from this table to
        // auto-apply the same category to future matching transactions.
        try
        {
            List<CategorizationHistoryEvent> historyEvents;

            if (!string.IsNullOrWhiteSpace(request.NormalizedDescription) && changedTransactions.Count > 0)
            {
                // When the caller supplies the normalized group key, record a
                // single history entry for the group. This produces a stronger
                // ML signal than N per-transaction entries and avoids inflating
                // MatchCount beyond what the user actually confirmed.
                historyEvents = new List<CategorizationHistoryEvent>
                {
                    new CategorizationHistoryEvent(
                        request.UserId,
                        request.NormalizedDescription,
                        request.CategoryId,
                        CategorizationHistorySource.Manual),
                };
            }
            else
            {
                historyEvents = changedTransactions
                    .Select(t => new CategorizationHistoryEvent(
                        request.UserId,
                        t.Description,
                        request.CategoryId,
                        CategorizationHistorySource.Manual))
                    .ToList();
            }

            if (historyEvents.Count > 0)
            {
                await _historyService.RecordCategorizationBatchAsync(historyEvents, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to record categorization history for bulk group categorize of {Count} transactions to category {CategoryId} (categorization was applied successfully)",
                changedTransactions.Count, request.CategoryId);
        }

        return new BulkCategorizeGroupResult
        {
            Success = errors.Count == 0,
            Message = errors.Count == 0
                ? $"Successfully categorized {updatedCount} transaction(s)"
                : $"Categorized {updatedCount} transaction(s) with {errors.Count} issue(s)",
            TransactionsUpdated = updatedCount,
            Errors = errors
        };
    }
}
