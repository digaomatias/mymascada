using MediatR;
using MyMascada.Application.Features.Budgets.DTOs;
using MyMascada.Application.Features.Budgets.Services;

namespace MyMascada.Application.Features.Budgets.Queries;

public class GetBudgetSuggestionsQuery : IRequest<IEnumerable<BudgetSuggestionDto>>
{
    public Guid UserId { get; set; }
    public int MonthsToAnalyze { get; set; } = 3;
}

public class GetBudgetSuggestionsQueryHandler : IRequestHandler<GetBudgetSuggestionsQuery, IEnumerable<BudgetSuggestionDto>>
{
    private readonly IBudgetCalculationService _calculationService;

    public GetBudgetSuggestionsQueryHandler(IBudgetCalculationService calculationService)
    {
        _calculationService = calculationService;
    }

    public async Task<IEnumerable<BudgetSuggestionDto>> Handle(GetBudgetSuggestionsQuery request, CancellationToken cancellationToken)
    {
        return await _calculationService.GenerateBudgetSuggestionsAsync(
            request.UserId,
            request.MonthsToAnalyze,
            cancellationToken);
    }
}
