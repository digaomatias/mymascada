using AutoMapper;
using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.DTOs;

namespace MyMascada.Application.Features.Transactions.Queries;

public class GetTransactionQuery : IRequest<TransactionDto?>
{
    public Guid UserId { get; set; }
    public int Id { get; set; }
}

public class GetTransactionQueryHandler : IRequestHandler<GetTransactionQuery, TransactionDto?>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IMapper _mapper;

    public GetTransactionQueryHandler(ITransactionRepository transactionRepository, IMapper mapper)
    {
        _transactionRepository = transactionRepository;
        _mapper = mapper;
    }

    public async Task<TransactionDto?> Handle(GetTransactionQuery request, CancellationToken cancellationToken)
    {
        var transaction = await _transactionRepository.GetByIdAsync(request.Id, request.UserId);
        if (transaction == null)
        {
            return null;
        }

        return _mapper.Map<TransactionDto>(transaction);
    }
}