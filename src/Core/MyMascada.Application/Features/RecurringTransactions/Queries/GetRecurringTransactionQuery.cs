using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RecurringTransactions.DTOs;
using MyMascada.Application.Features.RecurringTransactions.Mappings;

namespace MyMascada.Application.Features.RecurringTransactions.Queries;

public class GetRecurringTransactionQuery : IRequest<RecurringTransactionDetailDto?>
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
}

public class GetRecurringTransactionQueryHandler
    : IRequestHandler<GetRecurringTransactionQuery, RecurringTransactionDetailDto?>
{
    private readonly IRecurringTransactionRepository _recurringTransactionRepository;

    public GetRecurringTransactionQueryHandler(IRecurringTransactionRepository recurringTransactionRepository)
    {
        _recurringTransactionRepository = recurringTransactionRepository;
    }

    public async Task<RecurringTransactionDetailDto?> Handle(
        GetRecurringTransactionQuery request,
        CancellationToken cancellationToken)
    {
        var recurringTransaction = await _recurringTransactionRepository.GetByIdAsync(
            request.Id, request.UserId, cancellationToken);
        if (recurringTransaction == null)
        {
            return null;
        }

        var occurrences = await _recurringTransactionRepository.GetRecentOccurrencesAsync(
            request.Id, request.UserId, count: 10, cancellationToken);

        return RecurringTransactionMapper.ToDetailDto(recurringTransaction, occurrences);
    }
}
