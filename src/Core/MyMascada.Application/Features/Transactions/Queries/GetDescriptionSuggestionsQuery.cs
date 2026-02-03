using MediatR;
using MyMascada.Application.Common.Interfaces;

namespace MyMascada.Application.Features.Transactions.Queries;

public class GetDescriptionSuggestionsQuery : IRequest<IEnumerable<string>>
{
    public Guid UserId { get; set; }
    public string? SearchTerm { get; set; }
    public int Limit { get; set; } = 10;
}

public class GetDescriptionSuggestionsQueryHandler : IRequestHandler<GetDescriptionSuggestionsQuery, IEnumerable<string>>
{
    private readonly ITransactionRepository _transactionRepository;

    public GetDescriptionSuggestionsQueryHandler(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<IEnumerable<string>> Handle(GetDescriptionSuggestionsQuery request, CancellationToken cancellationToken)
    {
        return await _transactionRepository.GetUniqueDescriptionsAsync(
            request.UserId, 
            request.SearchTerm, 
            request.Limit);
    }
}