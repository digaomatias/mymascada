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

        var dtos = goalsList.Select(g => MapToSummaryDto(g, accountBalances)).ToList();

        // Server-side sort: inactive last, pinned first, journeyPriority, state urgency, daysRemaining
        var sorted = dtos
            .OrderBy(d => IsInactiveState(d.TrackingState) ? 1 : 0)
            .ThenByDescending(d => d.IsPinned)
            .ThenBy(d => d.JourneyPriority)
            .ThenBy(d => GetStateUrgency(d.TrackingState))
            .ThenBy(d => d.DaysRemaining.HasValue ? 0 : 1)
            .ThenBy(d => d.DaysRemaining ?? int.MaxValue)
            .ToList();

        for (var i = 0; i < sorted.Count; i++)
        {
            sorted[i].SortOrder = i;
        }

        return sorted;
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

        var status = goal.Status.ToString();
        var trackingState = GetTrackingState(status, goal.Deadline, daysRemaining, progressPercentage);
        var (journeyStage, journeyPriority) = GetJourney(goal.GoalType);
        var currentMilestone = GetCurrentMilestone(progressPercentage);
        var nextMilestone = currentMilestone < 100 ? currentMilestone + 25 : (int?)null;

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
            Status = status,
            Deadline = goal.Deadline,
            DaysRemaining = daysRemaining,
            LinkedAccountName = goal.Account?.Name,
            IsPinned = goal.IsPinned,
            TrackingState = trackingState,
            JourneyStage = journeyStage,
            JourneyPriority = journeyPriority,
            CurrentMilestone = currentMilestone > 0 ? currentMilestone : null,
            NextMilestone = nextMilestone
        };
    }

    private static string GetTrackingState(string status, DateTime? deadline, int? daysRemaining, decimal progressPercentage)
    {
        if (status == "Completed") return "completed";
        if (status is "Paused" or "Abandoned") return "paused";

        if (deadline.HasValue && daysRemaining.HasValue && daysRemaining.Value <= 0)
            return "overdue";

        if (deadline.HasValue && daysRemaining.HasValue && daysRemaining.Value > 0)
        {
            var remainingPct = 100m - progressPercentage;
            if ((remainingPct / daysRemaining.Value > 2.0m) ||
                (daysRemaining.Value <= 14 && progressPercentage < 80))
            {
                return "behind";
            }

            return "onTrack";
        }

        return "noDeadline";
    }

    private static (string Stage, int Priority) GetJourney(GoalType goalType) => goalType switch
    {
        GoalType.EmergencyFund => ("foundation", 1),
        GoalType.DebtPayoff => ("freedom", 2),
        GoalType.Investment or GoalType.Savings => ("growth", 3),
        _ => ("dreams", 4)
    };

    private static int GetCurrentMilestone(decimal progressPercentage)
    {
        return (int)(Math.Floor(progressPercentage / 25m) * 25);
    }

    private static bool IsInactiveState(string trackingState) =>
        trackingState is "completed" or "paused";

    private static int GetStateUrgency(string trackingState) => trackingState switch
    {
        "overdue" => 0,
        "behind" => 1,
        "onTrack" => 2,
        "noDeadline" => 3,
        _ => 4
    };
}
