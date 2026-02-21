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

    public GetGoalsQueryHandler(IGoalRepository goalRepository)
    {
        _goalRepository = goalRepository;
    }

    public async Task<IEnumerable<GoalSummaryDto>> Handle(GetGoalsQuery request, CancellationToken cancellationToken)
    {
        var goals = request.IncludeCompleted
            ? await _goalRepository.GetGoalsForUserAsync(request.UserId, cancellationToken)
            : await _goalRepository.GetActiveGoalsForUserAsync(request.UserId, cancellationToken);

        return goals.Select(MapToSummaryDto).ToList();
    }

    private static GoalSummaryDto MapToSummaryDto(Goal goal)
    {
        var now = DateTimeProvider.UtcNow;
        int? daysRemaining = null;
        if (goal.Deadline.HasValue && goal.Deadline.Value > now)
        {
            daysRemaining = (int)(goal.Deadline.Value.Date - now.Date).TotalDays;
        }

        return new GoalSummaryDto
        {
            Id = goal.Id,
            Name = goal.Name,
            Description = goal.Description,
            TargetAmount = goal.TargetAmount,
            CurrentAmount = goal.CurrentAmount,
            ProgressPercentage = goal.GetProgressPercentage(),
            RemainingAmount = goal.GetRemainingAmount(),
            GoalType = goal.GoalType.ToString(),
            Status = goal.Status.ToString(),
            Deadline = goal.Deadline,
            DaysRemaining = daysRemaining,
            LinkedAccountName = goal.Account?.Name
        };
    }
}
