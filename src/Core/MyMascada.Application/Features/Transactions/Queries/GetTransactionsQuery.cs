using AutoMapper;
using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Transactions.Queries;

public class GetTransactionsQuery : IRequest<TransactionListResponse>
{
    public Guid UserId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int? AccountId { get; set; }
    public int? CategoryId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public TransactionStatus? Status { get; set; }
    public string? SearchTerm { get; set; }
    public bool? IsReviewed { get; set; }
    public bool? IsReconciled { get; set; }
    public bool? IsExcluded { get; set; }
    public bool? NeedsCategorization { get; set; }
    public bool? IncludeTransfers { get; set; }
    public bool? OnlyTransfers { get; set; }
    public Guid? TransferId { get; set; }
    public string? TransactionType { get; set; }
    public string SortBy { get; set; } = "TransactionDate";
    public string SortDirection { get; set; } = "desc";
}

public class GetTransactionsQueryHandler : IRequestHandler<GetTransactionsQuery, TransactionListResponse>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IMapper _mapper;

    public GetTransactionsQueryHandler(ITransactionRepository transactionRepository, IMapper mapper)
    {
        _transactionRepository = transactionRepository;
        _mapper = mapper;
    }

    public async Task<TransactionListResponse> Handle(GetTransactionsQuery request, CancellationToken cancellationToken)
    {
        // Execute queries sequentially to avoid DbContext threading issues
        var (transactions, totalCount) = await _transactionRepository.GetFilteredAsync(request);
        var summary = await _transactionRepository.GetSummaryAsync(request);

        var transactionDtos = _mapper.Map<List<TransactionDto>>(transactions);

        return new TransactionListResponse
        {
            Transactions = transactionDtos,
            Summary = summary,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
        };
    }
}