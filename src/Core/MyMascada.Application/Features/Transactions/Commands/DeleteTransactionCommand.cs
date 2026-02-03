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

    public DeleteTransactionCommandHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<bool> Handle(DeleteTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = await _transactionRepository.GetByIdAsync(request.Id, request.UserId);
        if (transaction == null)
        {
            return false;
        }

        await _transactionRepository.DeleteAsync(transaction);
        return true;
    }
}