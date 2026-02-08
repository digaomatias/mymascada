using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.Transactions.Commands;

public class ReviewTransactionCommand : IRequest<bool>
{
    public Guid UserId { get; set; }
    public int TransactionId { get; set; }
}

public class ReviewTransactionCommandHandler : IRequestHandler<ReviewTransactionCommand, bool>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountAccessService _accountAccessService;

    public ReviewTransactionCommandHandler(
        ITransactionRepository transactionRepository,
        IAccountAccessService accountAccessService)
    {
        _transactionRepository = transactionRepository;
        _accountAccessService = accountAccessService;
    }

    public async Task<bool> Handle(ReviewTransactionCommand request, CancellationToken cancellationToken)
    {
        // Get the transaction and verify ownership
        var transaction = await _transactionRepository.GetByIdAsync(request.TransactionId, request.UserId);
        if (transaction == null)
        {
            return false;
        }

        // Verify the user has modify permission on the transaction's account (owner or Manager role)
        if (!await _accountAccessService.CanModifyAccountAsync(request.UserId, transaction.AccountId))
        {
            throw new UnauthorizedAccessException("You do not have permission to review transactions on this account.");
        }

        // Mark as reviewed
        transaction.IsReviewed = true;
        transaction.UpdatedAt = DateTime.UtcNow;

        await _transactionRepository.UpdateAsync(transaction);
        return true;
    }
}