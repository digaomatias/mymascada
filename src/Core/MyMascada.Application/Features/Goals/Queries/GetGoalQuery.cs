using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Goals.DTOs;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Goals.Queries;

public class GetGoalQuery : IRequest<GoalDetailDto?>
{
    public int GoalId { get; set; }
    public Guid UserId { get; set; }
}

public class GetGoalQueryHandler : IRequestHandler<GetGoalQuery, GoalDetailDto?>
{
    private readonly IGoalRepository _goalRepository;

    public GetGoalQueryHandler(IGoalRepository goalRepository)
    {
        _goalRepository = goalRepository;
    }

    public async Task<GoalDetailDto?> Handle(GetGoalQuery request, CancellationToken cancellationToken)
    {
        var goal = await _goalRepository.GetGoalByIdAsync(request.GoalId, request.UserId, cancellationToken);
        if (goal == null)
        {
            return null;
        }

        return MapToDetailDto(goal);
    }

    private static GoalDetailDto MapToDetailDto(Goal goal)
    {
        var now = DateTimeProvider.UtcNow;
        int? daysRemaining = null;
        if (goal.Deadline.HasValue && goal.Deadline.Value > now)
        {
            daysRemaining = (int)(goal.Deadline.Value.Date - now.Date).TotalDays;
        }

        return new GoalDetailDto
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
            LinkedAccountName = goal.Account?.Name,
            LinkedAccountId = goal.LinkedAccountId,
            DisplayOrder = goal.DisplayOrder,
            CreatedAt = goal.CreatedAt,
            UpdatedAt = goal.UpdatedAt
        };
    }
}
