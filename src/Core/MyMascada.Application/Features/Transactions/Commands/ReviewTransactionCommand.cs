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

    public ReviewTransactionCommandHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<bool> Handle(ReviewTransactionCommand request, CancellationToken cancellationToken)
    {
        // Get the transaction and verify ownership
        var transaction = await _transactionRepository.GetByIdAsync(request.TransactionId, request.UserId);
        if (transaction == null)
        {
            return false;
        }

        // Mark as reviewed
        transaction.IsReviewed = true;
        transaction.UpdatedAt = DateTime.UtcNow;

        await _transactionRepository.UpdateAsync(transaction);
        return true;
    }
}