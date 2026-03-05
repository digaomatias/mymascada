using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Goals.DTOs;
using MyMascada.Domain.Common;

namespace MyMascada.Application.Features.Goals.Queries;

public class GetCoachingInsightQuery : IRequest<CoachingInsightDto>
{
    public Guid UserId { get; set; }
}

public class GetCoachingInsightQueryHandler : IRequestHandler<GetCoachingInsightQuery, CoachingInsightDto>
{
    private readonly IGoalRepository _goalRepository;
    private readonly ITransactionRepository _transactionRepository;

    public GetCoachingInsightQueryHandler(
        IGoalRepository goalRepository,
        ITransactionRepository transactionRepository)
    {
        _goalRepository = goalRepository;
        _transactionRepository = transactionRepository;
    }

    public async Task<CoachingInsightDto> Handle(GetCoachingInsightQuery request, CancellationToken cancellationToken)
    {
        var now = DateTimeProvider.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endOfMonth = startOfMonth.AddMonths(1).AddSeconds(-1);

        var transactions = (await _transactionRepository.GetByDateRangeAsync(
            request.UserId, startOfMonth, endOfMonth)).ToList();

        var income = transactions.Where(t => t.IsIncome()).Sum(t => t.Amount);
        var expenses = transactions.Where(t => t.IsExpense()).Sum(t => Math.Abs(t.Amount));

        var goals = (await _goalRepository.GetActiveGoalsForUserAsync(request.UserId, cancellationToken)).ToList();

        // Priority 1: Overspending
        if (expenses > income && income > 0)
        {
            return new CoachingInsightDto
            {
                InsightKey = "overSpending",
                InsightParams = new Dictionary<string, string>
                {
                    { "income", income.ToString("F2") },
                    { "expenses", expenses.ToString("F2") }
                },
                InsightIcon = "alert-triangle",
                NudgeTone = "warning",
                NudgeKey = "overSpending.nudge"
            };
        }

        // Priority 2: No goals
        if (goals.Count == 0)
        {
            return new CoachingInsightDto
            {
                InsightKey = "noGoals",
                InsightIcon = "target",
                NudgeTone = "encourage",
                NudgeKey = "noGoals.nudge"
            };
        }

        // Priority 3: Behind schedule
        var behindGoal = FindBehindScheduleGoal(goals, now);
        if (behindGoal != null)
        {
            var remaining = behindGoal.Goal.TargetAmount - behindGoal.CurrentAmount;
            var daysLeft = behindGoal.DaysRemaining;
            var suggestedExtra = daysLeft > 0 ? Math.Round(remaining / daysLeft, 2) : remaining;

            return new CoachingInsightDto
            {
                InsightKey = "behindSchedule",
                InsightParams = new Dictionary<string, string>
                {
                    { "goalName", behindGoal.Goal.Name },
                    { "suggestedExtra", suggestedExtra.ToString("F2") }
                },
                InsightIcon = "clock",
                NudgeTone = "motivate",
                NudgeKey = "behindSchedule.nudge",
                NudgeTargetGoalId = behindGoal.Goal.Id
            };
        }

        // Priority 4: On track
        if (goals.Count > 0)
        {
            return new CoachingInsightDto
            {
                InsightKey = "onTrack",
                InsightParams = new Dictionary<string, string>
                {
                    { "goalCount", goals.Count.ToString() }
                },
                InsightIcon = "check-circle",
                NudgeTone = "positive",
                NudgeKey = "onTrack.nudge"
            };
        }

        // Priority 5: Default
        return new CoachingInsightDto
        {
            InsightKey = "default",
            InsightIcon = "lightbulb",
            NudgeTone = "neutral",
            NudgeKey = "default.nudge"
        };
    }

    private static BehindGoalInfo? FindBehindScheduleGoal(
        List<Domain.Entities.Goal> goals, DateTime now)
    {
        foreach (var goal in goals)
        {
            if (!goal.Deadline.HasValue) continue;
            if (goal.Deadline.Value <= now) continue;

            var daysRemaining = (int)(goal.Deadline.Value.Date - now.Date).TotalDays;
            var currentAmount = goal.CurrentAmount;
            var remaining = goal.TargetAmount - currentAmount;
            var progressPercentage = goal.TargetAmount > 0
                ? currentAmount / goal.TargetAmount * 100
                : 0m;

            var remainingPct = 100m - progressPercentage;

            var isBehind = (daysRemaining > 0 && remainingPct / daysRemaining > 2.0m)
                || (daysRemaining <= 14 && progressPercentage < 80);

            if (isBehind)
            {
                return new BehindGoalInfo(goal, currentAmount, daysRemaining);
            }
        }

        return null;
    }

    private sealed record BehindGoalInfo(Domain.Entities.Goal Goal, decimal CurrentAmount, int DaysRemaining);
}
