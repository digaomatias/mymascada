using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Budgets.DTOs;
using MyMascada.Application.Features.Budgets.Services;

namespace MyMascada.Application.Features.Budgets.Queries;

public class GetBudgetQuery : IRequest<BudgetDetailDto?>
{
    public int BudgetId { get; set; }
    public Guid UserId { get; set; }
}

public class GetBudgetQueryHandler : IRequestHandler<GetBudgetQuery, BudgetDetailDto?>
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly IBudgetCalculationService _calculationService;

    public GetBudgetQueryHandler(
        IBudgetRepository budgetRepository,
        IBudgetCalculationService calculationService)
    {
        _budgetRepository = budgetRepository;
        _calculationService = calculationService;
    }

    public async Task<BudgetDetailDto?> Handle(GetBudgetQuery request, CancellationToken cancellationToken)
    {
        var budget = await _budgetRepository.GetBudgetByIdAsync(request.BudgetId, request.UserId, cancellationToken);
        if (budget == null)
        {
            return null;
        }

        return await _calculationService.CalculateBudgetProgressAsync(budget, request.UserId, cancellationToken);
    }
}
