using MediatR;
using MyMascada.Application.Features.Rules.DTOs;
using MyMascada.Application.Features.Rules.Queries;
using MyMascada.Application.Features.Rules.Services;

namespace MyMascada.Application.Features.Rules.Handlers;

public class GetRuleSuggestionsQueryHandler : IRequestHandler<GetRuleSuggestionsQuery, RuleSuggestionsResponse>
{
    private readonly IRuleSuggestionsService _suggestionsService;

    public GetRuleSuggestionsQueryHandler(IRuleSuggestionsService suggestionsService)
    {
        _suggestionsService = suggestionsService;
    }

    public async Task<RuleSuggestionsResponse> Handle(GetRuleSuggestionsQuery request, CancellationToken cancellationToken)
    {
        return await _suggestionsService.GenerateSuggestionsAsync(request.UserId, cancellationToken);
    }
}

public class AnalyzeTransactionsForRulesQueryHandler : IRequestHandler<AnalyzeTransactionsForRulesQuery, List<RuleSuggestionDto>>
{
    private readonly IRuleSuggestionsService _suggestionsService;

    public AnalyzeTransactionsForRulesQueryHandler(IRuleSuggestionsService suggestionsService)
    {
        _suggestionsService = suggestionsService;
    }

    public async Task<List<RuleSuggestionDto>> Handle(AnalyzeTransactionsForRulesQuery request, CancellationToken cancellationToken)
    {
        return await _suggestionsService.AnalyzeTransactionsAsync(request.UserId, request.TransactionIds, cancellationToken);
    }
}