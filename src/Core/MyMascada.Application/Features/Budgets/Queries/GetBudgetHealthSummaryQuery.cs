using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Budgets.DTOs;
using MyMascada.Application.Features.Budgets.Services;

namespace MyMascada.Application.Features.Budgets.Queries;

public class GetBudgetHealthSummaryQuery : IRequest<BudgetHealthSummaryDto>
{
    public Guid UserId { get; set; }
}

public class GetBudgetHealthSummaryQueryHandler : IRequestHandler<GetBudgetHealthSummaryQuery, BudgetHealthSummaryDto>
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly IBudgetCalculationService _calculationService;

    public GetBudgetHealthSummaryQueryHandler(
        IBudgetRepository budgetRepository,
        IBudgetCalculationService calculationService)
    {
        _budgetRepository = budgetRepository;
        _calculationService = calculationService;
    }

    public async Task<BudgetHealthSummaryDto> Handle(GetBudgetHealthSummaryQuery request, CancellationToken cancellationToken)
    {
        var budgets = await _budgetRepository.GetActiveBudgetsForUserAsync(request.UserId, cancellationToken);
        var budgetList = budgets.ToList();

        var items = new List<BudgetRiskItemDto>();
        decimal totalBudgeted = 0;
        decimal totalSpent = 0;
        int? nearestDeadlineDays = null;

        foreach (var budget in budgetList)
        {
            var loaded = await _budgetRepository.GetBudgetByIdAsync(budget.Id, request.UserId, cancellationToken);
            if (loaded == null) continue;

            var summary = await _calculationService.ToBudgetSummaryAsync(loaded, request.UserId, cancellationToken);

            var riskState = GetRiskState(summary.UsedPercentage, summary.IsActive);
            var priorityScore = GetPriorityScore(riskState);

            var periodElapsed = loaded.GetPeriodElapsedPercentage();
            var expectedSpent = summary.TotalBudgeted * (periodElapsed / 100m);
            var variance = summary.TotalSpent - expectedSpent;
            var variancePercentage = expectedSpent > 0
                ? Math.Round(variance / expectedSpent * 100, 2)
                : 0m;

            items.Add(new BudgetRiskItemDto
            {
                BudgetId = summary.Id,
                Name = summary.Name,
                RiskState = riskState,
                PriorityScore = priorityScore,
                ExpectedSpent = Math.Round(expectedSpent, 2),
                Variance = Math.Round(variance, 2),
                VariancePercentage = variancePercentage,
                IsOverspendingPace = variance > 0
            });

            totalBudgeted += summary.TotalBudgeted;
            totalSpent += summary.TotalSpent;

            if (summary.DaysRemaining > 0)
            {
                if (!nearestDeadlineDays.HasValue || summary.DaysRemaining < nearestDeadlineDays.Value)
                {
                    nearestDeadlineDays = summary.DaysRemaining;
                }
            }
        }

        var sortedItems = items.OrderByDescending(i => i.PriorityScore).ToList();

        var overallPercentage = totalBudgeted > 0
            ? Math.Round(totalSpent / totalBudgeted * 100, 2)
            : 0m;

        return new BudgetHealthSummaryDto
        {
            TotalBudgeted = totalBudgeted,
            TotalSpent = totalSpent,
            OverallPercentage = overallPercentage,
            BudgetsOverLimit = items.Count(i => i.RiskState == "over"),
            BudgetsApproaching = items.Count(i => i.RiskState == "risk"),
            OverCount = items.Count(i => i.RiskState == "over"),
            AtRiskCount = items.Count(i => i.RiskState == "risk"),
            OnTrackCount = items.Count(i => i.RiskState == "onTrack"),
            InactiveCount = items.Count(i => i.RiskState == "inactive"),
            NearestDeadlineDays = nearestDeadlineDays,
            Items = sortedItems
        };
    }

    private static string GetRiskState(decimal usedPercentage, bool isActive)
    {
        if (usedPercentage >= 100) return "over";
        if (usedPercentage >= 80) return "risk";
        if (isActive) return "onTrack";
        return "inactive";
    }

    private static int GetPriorityScore(string riskState) => riskState switch
    {
        "over" => 400,
        "risk" => 300,
        "onTrack" => 200,
        _ => 100
    };
}
