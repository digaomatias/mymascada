using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Budgets.DTOs;
using MyMascada.Application.Features.Budgets.Services;

namespace MyMascada.Application.Features.Budgets.Queries;

public class GetBudgetsQuery : IRequest<IEnumerable<BudgetSummaryDto>>
{
    public Guid UserId { get; set; }
    public bool IncludeInactive { get; set; } = false;
    public bool OnlyCurrentPeriod { get; set; } = false;
}

public class GetBudgetsQueryHandler : IRequestHandler<GetBudgetsQuery, IEnumerable<BudgetSummaryDto>>
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly IBudgetCalculationService _calculationService;

    public GetBudgetsQueryHandler(
        IBudgetRepository budgetRepository,
        IBudgetCalculationService calculationService)
    {
        _budgetRepository = budgetRepository;
        _calculationService = calculationService;
    }

    public async Task<IEnumerable<BudgetSummaryDto>> Handle(GetBudgetsQuery request, CancellationToken cancellationToken)
    {
        var budgets = await _budgetRepository.GetBudgetsForUserAsync(request.UserId, cancellationToken);

        // Filter by active status
        if (!request.IncludeInactive)
        {
            budgets = budgets.Where(b => b.IsActive);
        }

        // Filter to current period only
        if (request.OnlyCurrentPeriod)
        {
            var today = DateTime.UtcNow;
            budgets = budgets.Where(b => b.ContainsDate(today));
        }

        // Convert to summaries with calculated progress
        var summaries = new List<BudgetSummaryDto>();
        foreach (var budget in budgets.ToList())
        {
            // Load categories if not already loaded
            var loadedBudget = await _budgetRepository.GetBudgetByIdAsync(budget.Id, request.UserId, cancellationToken);
            if (loadedBudget != null)
            {
                var summary = await _calculationService.ToBudgetSummaryAsync(loadedBudget, request.UserId, cancellationToken);
                summaries.Add(summary);
            }
        }

        // Sort by start date descending (most recent first), then by name
        return summaries
            .OrderByDescending(b => b.StartDate)
            .ThenBy(b => b.Name);
    }
}
