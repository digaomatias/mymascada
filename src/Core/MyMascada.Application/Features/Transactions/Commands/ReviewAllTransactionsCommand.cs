using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.Transactions.Commands;

public class ReviewAllTransactionsCommand : IRequest<ReviewAllTransactionsResponse>
{
    public Guid UserId { get; set; }
}

public class ReviewAllTransactionsResponse
{
    public int ReviewedCount { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ReviewAllTransactionsCommandHandler : IRequestHandler<ReviewAllTransactionsCommand, ReviewAllTransactionsResponse>
{
    private readonly ITransactionRepository _transactionRepository;

    public ReviewAllTransactionsCommandHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<ReviewAllTransactionsResponse> Handle(ReviewAllTransactionsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Get all unreviewed transactions for the user
            var unreviewedTransactions = await _transactionRepository.GetUnreviewedAsync(request.UserId);
            
            if (!unreviewedTransactions.Any())
            {
                return new ReviewAllTransactionsResponse
                {
                    Success = true,
                    ReviewedCount = 0,
                    Message = "No transactions need review"
                };
            }

            // Mark all as reviewed
            foreach (var transaction in unreviewedTransactions)
            {
                transaction.IsReviewed = true;
                transaction.UpdatedAt = DateTime.UtcNow;
                await _transactionRepository.UpdateAsync(transaction);
            }

            return new ReviewAllTransactionsResponse
            {
                Success = true,
                ReviewedCount = unreviewedTransactions.Count(),
                Message = $"Successfully reviewed {unreviewedTransactions.Count()} transaction(s)"
            };
        }
        catch (Exception ex)
        {
            return new ReviewAllTransactionsResponse
            {
                Success = false,
                ReviewedCount = 0,
                Message = $"Failed to review transactions: {ex.Message}"
            };
        }
    }
}