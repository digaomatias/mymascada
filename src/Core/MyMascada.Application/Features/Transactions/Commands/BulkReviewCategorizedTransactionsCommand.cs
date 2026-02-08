using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using System.Linq;

namespace MyMascada.Application.Features.Transactions.Commands;

public class BulkReviewCategorizedTransactionsCommand : IRequest<BulkReviewCategorizedTransactionsResult>
{
    public Guid UserId { get; set; }
    public int? AccountId { get; set; }
    public string? SearchText { get; set; }
}

public class BulkReviewCategorizedTransactionsResult
{
    public int ReviewedCount { get; set; }
    public int TotalProcessed { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class BulkReviewCategorizedTransactionsCommandHandler : IRequestHandler<BulkReviewCategorizedTransactionsCommand, BulkReviewCategorizedTransactionsResult>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountAccessService _accountAccessService;
    private readonly IApplicationLogger<BulkReviewCategorizedTransactionsCommandHandler> _logger;

    public BulkReviewCategorizedTransactionsCommandHandler(
        ITransactionRepository transactionRepository,
        IAccountAccessService accountAccessService,
        IApplicationLogger<BulkReviewCategorizedTransactionsCommandHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _accountAccessService = accountAccessService;
        _logger = logger;
    }

    public async Task<BulkReviewCategorizedTransactionsResult> Handle(
        BulkReviewCategorizedTransactionsCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting bulk review of categorized transactions for user {UserId}", request.UserId);

            // If a specific account is targeted, verify modify permission upfront
            if (request.AccountId.HasValue)
            {
                if (!await _accountAccessService.CanModifyAccountAsync(request.UserId, request.AccountId.Value))
                {
                    throw new UnauthorizedAccessException("You do not have permission to review transactions on this account.");
                }
            }

            // Get all unreviewed transactions for the user
            var unreviewedTransactions = await _transactionRepository.GetUnreviewedAsync(request.UserId);

            // Filter for transactions that have a category assigned
            var transactionsToReview = unreviewedTransactions
                .Where(t => t.CategoryId.HasValue) // Only transactions with categories
                .Where(t => t.TransferId == null && t.Type != Domain.Enums.TransactionType.TransferComponent); // Exclude transfers

            // Apply optional filters
            if (request.AccountId.HasValue)
            {
                transactionsToReview = transactionsToReview.Where(t => t.AccountId == request.AccountId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchText))
            {
                var searchLower = request.SearchText.ToLower();
                transactionsToReview = transactionsToReview.Where(t =>
                    t.Description.ToLower().Contains(searchLower) ||
                    (t.UserDescription != null && t.UserDescription.ToLower().Contains(searchLower)));
            }

            // Only include transactions on accounts the user can modify (owner or Manager role)
            var allTransactions = transactionsToReview.ToList();
            var transactionsList = new List<Domain.Entities.Transaction>();
            foreach (var transaction in allTransactions)
            {
                if (await _accountAccessService.CanModifyAccountAsync(request.UserId, transaction.AccountId))
                {
                    transactionsList.Add(transaction);
                }
            }
            var totalProcessed = transactionsList.Count;

            if (totalProcessed == 0)
            {
                return new BulkReviewCategorizedTransactionsResult
                {
                    ReviewedCount = 0,
                    TotalProcessed = 0,
                    Success = true,
                    Message = "No categorized transactions found that need review"
                };
            }

            // Mark all as reviewed
            foreach (var transaction in transactionsList)
            {
                transaction.IsReviewed = true;
                transaction.UpdatedAt = DateTime.UtcNow;
                await _transactionRepository.UpdateAsync(transaction);
            }

            // No need to call SaveChangesAsync as UpdateAsync handles it per the existing pattern

            _logger.LogInformation("Successfully reviewed {Count} categorized transactions", totalProcessed);

            return new BulkReviewCategorizedTransactionsResult
            {
                ReviewedCount = totalProcessed,
                TotalProcessed = totalProcessed,
                Success = true,
                Message = $"Successfully reviewed {totalProcessed} categorized transaction{(totalProcessed != 1 ? "s" : "")}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk review of categorized transactions");
            return new BulkReviewCategorizedTransactionsResult
            {
                ReviewedCount = 0,
                TotalProcessed = 0,
                Success = false,
                Message = "An error occurred while reviewing transactions"
            };
        }
    }
}