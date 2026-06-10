using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RecurringTransactions.DTOs;
using MyMascada.Application.Features.RecurringTransactions.Mappings;

namespace MyMascada.Application.Features.RecurringTransactions.Queries;

public class GetRecurringTransactionsQuery : IRequest<List<RecurringTransactionDto>>
{
    public Guid UserId { get; set; }
    public bool IncludeInactive { get; set; }
}

public class GetRecurringTransactionsQueryHandler
    : IRequestHandler<GetRecurringTransactionsQuery, List<RecurringTransactionDto>>
{
    private readonly IRecurringTransactionRepository _recurringTransactionRepository;

    public GetRecurringTransactionsQueryHandler(IRecurringTransactionRepository recurringTransactionRepository)
    {
        _recurringTransactionRepository = recurringTransactionRepository;
    }

    public async Task<List<RecurringTransactionDto>> Handle(
        GetRecurringTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        var recurringTransactions = await _recurringTransactionRepository.GetByUserIdAsync(
            request.UserId, request.IncludeInactive, cancellationToken);

        return recurringTransactions.Select(RecurringTransactionMapper.ToDto).ToList();
    }
}
