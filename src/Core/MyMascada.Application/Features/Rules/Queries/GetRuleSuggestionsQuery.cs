using MediatR;
using MyMascada.Application.Features.Rules.DTOs;

namespace MyMascada.Application.Features.Rules.Queries;

public class GetRuleSuggestionsQuery : IRequest<RuleSuggestionsResponse>
{
    public Guid UserId { get; set; }
}

public class AnalyzeTransactionsForRulesQuery : IRequest<List<RuleSuggestionDto>>
{
    public Guid UserId { get; set; }
    public List<int> TransactionIds { get; set; } = new();
}