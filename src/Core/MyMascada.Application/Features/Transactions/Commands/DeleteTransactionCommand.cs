using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.Transactions.Commands;

public class DeleteTransactionCommand : IRequest<bool>
{
    public Guid UserId { get; set; }
    public int Id { get; set; }
}

public class DeleteTransactionCommandHandler : IRequestHandler<DeleteTransactionCommand, bool>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IAccountAccessService _accountAccessService;

    public DeleteTransactionCommandHandler(
        ITransactionRepository transactionRepository,
        IAccountAccessService accountAccessService)
    {
        _transactionRepository = transactionRepository;
        _accountAccessService = accountAccessService;
    }

    public async Task<bool> Handle(DeleteTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = await _transactionRepository.GetByIdAsync(request.Id, request.UserId);
        if (transaction == null)
        {
            return false;
        }

        // Verify the user has modify permission on the transaction's account (owner or Manager role)
        if (!await _accountAccessService.CanModifyAccountAsync(request.UserId, transaction.AccountId))
        {
            throw new UnauthorizedAccessException("You do not have permission to delete transactions on this account.");
        }

        await _transactionRepository.DeleteAsync(transaction);
        return true;
    }
}