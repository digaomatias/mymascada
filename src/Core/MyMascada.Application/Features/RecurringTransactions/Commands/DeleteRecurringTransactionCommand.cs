using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.RecurringTransactions.Commands;

public class DeleteRecurringTransactionCommand : IRequest<bool>
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
}

public class DeleteRecurringTransactionCommandHandler : IRequestHandler<DeleteRecurringTransactionCommand, bool>
{
    private readonly IRecurringTransactionRepository _recurringTransactionRepository;

    public DeleteRecurringTransactionCommandHandler(IRecurringTransactionRepository recurringTransactionRepository)
    {
        _recurringTransactionRepository = recurringTransactionRepository;
    }

    public async Task<bool> Handle(DeleteRecurringTransactionCommand request, CancellationToken cancellationToken)
    {
        return await _recurringTransactionRepository.DeleteAsync(request.Id, request.UserId, cancellationToken);
    }
}
