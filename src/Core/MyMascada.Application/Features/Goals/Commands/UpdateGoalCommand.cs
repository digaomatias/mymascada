using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Goals.DTOs;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Goals.Commands;

public class UpdateGoalCommand : IRequest<GoalDetailDto>
{
    public int GoalId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? TargetAmount { get; set; }
    public decimal? CurrentAmount { get; set; }
    public string? Status { get; set; }
    public DateTime? Deadline { get; set; }
    public int? LinkedAccountId { get; set; }
    public bool ClearLinkedAccount { get; set; }
    public Guid UserId { get; set; }
}

public class UpdateGoalCommandHandler : IRequestHandler<UpdateGoalCommand, GoalDetailDto>
{
    private readonly IGoalRepository _goalRepository;
    private readonly IAccountRepository _accountRepository;

    public UpdateGoalCommandHandler(
        IGoalRepository goalRepository,
        IAccountRepository accountRepository)
    {
        _goalRepository = goalRepository;
        _accountRepository = accountRepository;
    }

    public async Task<GoalDetailDto> Handle(UpdateGoalCommand request, CancellationToken cancellationToken)
    {
        var goal = await _goalRepository.GetGoalByIdAsync(request.GoalId, request.UserId, cancellationToken);
        if (goal == null)
        {
            throw new ArgumentException("Goal not found or you don't have permission to access it.");
        }

        if (request.Name != null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Goal name cannot be empty.");
            }

            if (await _goalRepository.GoalNameExistsAsync(request.UserId, request.Name.Trim(), request.GoalId, cancellationToken))
            {
                throw new ArgumentException($"A goal with the name '{request.Name.Trim()}' already exists.");
            }

            goal.Name = request.Name.Trim();
        }

        if (request.Description != null)
        {
            goal.Description = request.Description.Trim();
        }

        if (request.TargetAmount.HasValue)
        {
            if (request.TargetAmount.Value <= 0)
            {
                throw new ArgumentException("Target amount must be greater than zero.");
            }
            goal.TargetAmount = request.TargetAmount.Value;
        }

        if (request.CurrentAmount.HasValue)
        {
            if (request.CurrentAmount.Value < 0)
            {
                throw new ArgumentException("Current amount cannot be negative.");
            }
            goal.CurrentAmount = request.CurrentAmount.Value;
        }

        if (request.Status != null)
        {
            if (!Enum.TryParse<GoalStatus>(request.Status, true, out var status))
            {
                throw new ArgumentException($"Invalid status: {request.Status}. Valid values are: Active, Completed, Paused, Abandoned");
            }

            switch (status)
            {
                case GoalStatus.Completed:
                    goal.MarkCompleted();
                    break;
                case GoalStatus.Paused:
                    goal.MarkPaused();
                    break;
                case GoalStatus.Abandoned:
                    goal.MarkAbandoned();
                    break;
                default:
                    goal.Status = status;
                    break;
            }
        }

        if (request.Deadline.HasValue)
        {
            goal.Deadline = EnsureUtc(request.Deadline.Value);
        }

        if (request.LinkedAccountId.HasValue)
        {
            var account = await _accountRepository.GetByIdAsync(request.LinkedAccountId.Value, request.UserId);
            if (account == null)
            {
                throw new ArgumentException("Linked account not found or you don't have permission to access it.");
            }
            goal.LinkedAccountId = request.LinkedAccountId.Value;
        }
        else if (request.ClearLinkedAccount)
        {
            goal.LinkedAccountId = null;
        }

        // Auto-complete: if CurrentAmount >= TargetAmount after update
        if (goal.Status == GoalStatus.Active && goal.CurrentAmount >= goal.TargetAmount)
        {
            goal.MarkCompleted();
        }

        var updatedGoal = await _goalRepository.UpdateGoalAsync(goal, cancellationToken);
        return MapToDetailDto(updatedGoal);
    }

    private static DateTime EnsureUtc(DateTime dateTime) => dateTime.Kind switch
    {
        DateTimeKind.Utc => dateTime,
        DateTimeKind.Local => dateTime.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
        _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
    };

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
