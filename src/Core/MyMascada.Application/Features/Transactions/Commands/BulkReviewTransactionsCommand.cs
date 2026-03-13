using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.Transactions.Commands;

public class BulkReviewTransactionsCommand : IRequest<BulkReviewTransactionsResult>
{
    public Guid UserId { get; set; }
    public List<int> TransactionIds { get; set; } = new();
}

public class BulkReviewTransactionsResult
{
    public int ReviewedCount { get; set; }
    public int TotalProcessed { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class BulkReviewTransactionsCommandHandler : IRequestHandler<BulkReviewTransactionsCommand, BulkReviewTransactionsResult>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountAccessService _accountAccessService;
    private readonly IApplicationLogger<BulkReviewTransactionsCommandHandler> _logger;

    public BulkReviewTransactionsCommandHandler(
        ITransactionRepository transactionRepository,
        IAccountAccessService accountAccessService,
        IApplicationLogger<BulkReviewTransactionsCommandHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _accountAccessService = accountAccessService;
        _logger = logger;
    }

    public async Task<BulkReviewTransactionsResult> Handle(
        BulkReviewTransactionsCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting bulk review of {Count} transactions for user {UserId}", request.TransactionIds.Count, request.UserId);

            if (request.TransactionIds.Count == 0)
            {
                return new BulkReviewTransactionsResult
                {
                    ReviewedCount = 0,
                    TotalProcessed = 0,
                    Success = true,
                    Message = "No transaction IDs provided"
                };
            }

            var reviewedCount = 0;
            var totalProcessed = 0;

            foreach (var transactionId in request.TransactionIds)
            {
                var transaction = await _transactionRepository.GetByIdAsync(transactionId, request.UserId);
                if (transaction == null)
                {
                    continue;
                }

                if (!await _accountAccessService.CanModifyAccountAsync(request.UserId, transaction.AccountId))
                {
                    continue;
                }

                totalProcessed++;

                if (transaction.IsReviewed)
                {
                    continue;
                }

                transaction.IsReviewed = true;
                transaction.UpdatedAt = DateTime.UtcNow;
                await _transactionRepository.UpdateAsync(transaction);
                reviewedCount++;
            }

            _logger.LogInformation("Successfully reviewed {ReviewedCount} of {TotalProcessed} transactions", reviewedCount, totalProcessed);

            return new BulkReviewTransactionsResult
            {
                ReviewedCount = reviewedCount,
                TotalProcessed = totalProcessed,
                Success = true,
                Message = $"Successfully reviewed {reviewedCount} transaction{(reviewedCount != 1 ? "s" : "")}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk review of transactions");
            return new BulkReviewTransactionsResult
            {
                ReviewedCount = 0,
                TotalProcessed = 0,
                Success = false,
                Message = "An error occurred while reviewing transactions"
            };
        }
    }
}
