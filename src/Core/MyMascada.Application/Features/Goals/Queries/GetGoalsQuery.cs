using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Goals.DTOs;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Goals.Queries;

public class GetGoalsQuery : IRequest<IEnumerable<GoalSummaryDto>>
{
    public Guid UserId { get; set; }
    public bool IncludeCompleted { get; set; } = false;
}

public class GetGoalsQueryHandler : IRequestHandler<GetGoalsQuery, IEnumerable<GoalSummaryDto>>
{
    private readonly IGoalRepository _goalRepository;
    private readonly ITransactionRepository _transactionRepository;

    public GetGoalsQueryHandler(IGoalRepository goalRepository, ITransactionRepository transactionRepository)
    {
        _goalRepository = goalRepository;
        _transactionRepository = transactionRepository;
    }

    public async Task<IEnumerable<GoalSummaryDto>> Handle(GetGoalsQuery request, CancellationToken cancellationToken)
    {
        var goals = request.IncludeCompleted
            ? await _goalRepository.GetGoalsForUserAsync(request.UserId, cancellationToken)
            : await _goalRepository.GetActiveGoalsForUserAsync(request.UserId, cancellationToken);

        var goalsList = goals.ToList();

        // Batch-load account balances for goals with linked accounts
        var linkedAccountIds = goalsList
            .Where(g => g.LinkedAccountId.HasValue)
            .Select(g => g.LinkedAccountId!.Value)
            .Distinct()
            .ToList();

        Dictionary<int, decimal> accountBalances = new();
        if (linkedAccountIds.Count > 0)
        {
            accountBalances = await _transactionRepository.GetAccountBalancesAsync(request.UserId);
        }

        return goalsList.Select(g => MapToSummaryDto(g, accountBalances)).ToList();
    }

    private static GoalSummaryDto MapToSummaryDto(Goal goal, Dictionary<int, decimal> accountBalances)
    {
        var now = DateTimeProvider.UtcNow;
        int? daysRemaining = null;
        if (goal.Deadline.HasValue && goal.Deadline.Value > now)
        {
            daysRemaining = (int)(goal.Deadline.Value.Date - now.Date).TotalDays;
        }

        var currentAmount = goal.LinkedAccountId.HasValue
            ? accountBalances.GetValueOrDefault(goal.LinkedAccountId.Value, 0m)
            : goal.CurrentAmount;

        var progressPercentage = goal.TargetAmount > 0
            ? Math.Round((currentAmount / goal.TargetAmount) * 100, 2)
            : 0;

        var remainingAmount = Math.Max(goal.TargetAmount - currentAmount, 0);

        return new GoalSummaryDto
        {
            Id = goal.Id,
            Name = goal.Name,
            Description = goal.Description,
            TargetAmount = goal.TargetAmount,
            CurrentAmount = currentAmount,
            ProgressPercentage = progressPercentage,
            RemainingAmount = remainingAmount,
            GoalType = goal.GoalType.ToString(),
            Status = goal.Status.ToString(),
            Deadline = goal.Deadline,
            DaysRemaining = daysRemaining,
            LinkedAccountName = goal.Account?.Name,
            IsPinned = goal.IsPinned
        };
    }
}
