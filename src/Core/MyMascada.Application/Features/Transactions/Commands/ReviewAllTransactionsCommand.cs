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
    private readonly IAccountAccessService _accountAccessService;

    public ReviewAllTransactionsCommandHandler(
        ITransactionRepository transactionRepository,
        IAccountAccessService accountAccessService)
    {
        _transactionRepository = transactionRepository;
        _accountAccessService = accountAccessService;
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

            // Only review transactions on accounts the user can modify (owner or Manager role)
            var modifiableTransactions = new List<Domain.Entities.Transaction>();
            foreach (var transaction in unreviewedTransactions)
            {
                if (await _accountAccessService.CanModifyAccountAsync(request.UserId, transaction.AccountId))
                {
                    modifiableTransactions.Add(transaction);
                }
            }

            if (!modifiableTransactions.Any())
            {
                return new ReviewAllTransactionsResponse
                {
                    Success = true,
                    ReviewedCount = 0,
                    Message = "No transactions need review"
                };
            }

            // Mark all modifiable as reviewed
            foreach (var transaction in modifiableTransactions)
            {
                transaction.IsReviewed = true;
                transaction.UpdatedAt = DateTime.UtcNow;
                await _transactionRepository.UpdateAsync(transaction);
            }

            return new ReviewAllTransactionsResponse
            {
                Success = true,
                ReviewedCount = modifiableTransactions.Count,
                Message = $"Successfully reviewed {modifiableTransactions.Count} transaction(s)"
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