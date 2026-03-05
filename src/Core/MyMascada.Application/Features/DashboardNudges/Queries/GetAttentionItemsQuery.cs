using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Budgets.Services;
using MyMascada.Application.Features.DashboardNudges.DTOs;
using MyMascada.Application.Features.UpcomingBills.Queries;

namespace MyMascada.Application.Features.DashboardNudges.Queries;

public class GetAttentionItemsQuery : IRequest<AttentionItemsDto>
{
    public Guid UserId { get; set; }
}

public class GetAttentionItemsQueryHandler : IRequestHandler<GetAttentionItemsQuery, AttentionItemsDto>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly IBudgetCalculationService _budgetCalculationService;
    private readonly IMediator _mediator;

    public GetAttentionItemsQueryHandler(
        ITransactionRepository transactionRepository,
        IBudgetRepository budgetRepository,
        IBudgetCalculationService budgetCalculationService,
        IMediator mediator)
    {
        _transactionRepository = transactionRepository;
        _budgetRepository = budgetRepository;
        _budgetCalculationService = budgetCalculationService;
        _mediator = mediator;
    }

    public async Task<AttentionItemsDto> Handle(GetAttentionItemsQuery request, CancellationToken cancellationToken)
    {
        // Run 3 queries in parallel
        var uncategorizedTask = GetUncategorizedItemAsync(request.UserId, cancellationToken);
        var upcomingBillsTask = GetUpcomingBillsItemsAsync(request.UserId, cancellationToken);
        var overBudgetTask = GetOverBudgetItemsAsync(request.UserId, cancellationToken);

        await Task.WhenAll(uncategorizedTask, upcomingBillsTask, overBudgetTask);

        var items = new List<AttentionItemDto>();

        var uncategorized = await uncategorizedTask;
        if (uncategorized != null)
            items.Add(uncategorized);

        items.AddRange(await upcomingBillsTask);
        items.AddRange(await overBudgetTask);

        // Sort by severity (error > warn > info) then take max 4
        var severityOrder = new Dictionary<string, int>
        {
            ["error"] = 0,
            ["warn"] = 1,
            ["info"] = 2
        };

        var sortedItems = items
            .OrderBy(i => severityOrder.GetValueOrDefault(i.Severity, 3))
            .Take(4)
            .ToList();

        return new AttentionItemsDto { Items = sortedItems };
    }

    private async Task<AttentionItemDto?> GetUncategorizedItemAsync(Guid userId, CancellationToken cancellationToken)
    {
        var uncategorized = await _transactionRepository.GetUncategorizedTransactionsAsync(userId, 500, cancellationToken);
        var count = uncategorized.Count();

        if (count == 0)
            return null;

        return new AttentionItemDto
        {
            Type = "uncategorized_transactions",
            Severity = count > 20 ? "warn" : "info",
            Count = count
        };
    }

    private async Task<List<AttentionItemDto>> GetUpcomingBillsItemsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var billsResponse = await _mediator.Send(new GetUpcomingBillsQuery
        {
            UserId = userId,
            DaysAhead = 7
        }, cancellationToken);

        var items = new List<AttentionItemDto>();

        foreach (var bill in billsResponse.Bills)
        {
            items.Add(new AttentionItemDto
            {
                Type = "upcoming_bill",
                Severity = bill.DaysUntilDue <= 2 ? "warn" : "info",
                EntityName = bill.MerchantName,
                Amount = bill.ExpectedAmount,
                DaysUntilDue = bill.DaysUntilDue,
                AnnualizedAmount = bill.ExpectedAmount * 12
            });
        }

        return items;
    }

    private async Task<List<AttentionItemDto>> GetOverBudgetItemsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var activeBudgets = await _budgetRepository.GetActiveBudgetsForUserAsync(userId, cancellationToken);
        var items = new List<AttentionItemDto>();

        foreach (var budget in activeBudgets)
        {
            var summary = await _budgetCalculationService.ToBudgetSummaryAsync(budget, userId, cancellationToken);

            if (summary.UsedPercentage >= 100)
            {
                items.Add(new AttentionItemDto
                {
                    Type = "over_budget",
                    Severity = "error",
                    EntityName = summary.Name,
                    Amount = summary.TotalSpent - summary.TotalBudgeted
                });
            }
            else if (summary.UsedPercentage >= 90)
            {
                items.Add(new AttentionItemDto
                {
                    Type = "approaching_budget",
                    Severity = "warn",
                    EntityName = summary.Name,
                    Amount = summary.TotalRemaining
                });
            }
        }

        return items;
    }
}
