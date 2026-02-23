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
    private readonly ITransactionRepository _transactionRepository;

    public UpdateGoalCommandHandler(
        IGoalRepository goalRepository,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository)
    {
        _goalRepository = goalRepository;
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
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

        // Only allow manual CurrentAmount updates for goals without a linked account.
        // Linked goals derive their current amount from the account balance.
        if (request.CurrentAmount.HasValue && !goal.LinkedAccountId.HasValue)
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

        // Look up live balance for linked account to check auto-complete
        decimal? linkedAccountBalance = null;
        if (goal.LinkedAccountId.HasValue)
        {
            var accountBalances = await _transactionRepository.GetAccountBalancesAsync(request.UserId);
            linkedAccountBalance = accountBalances.GetValueOrDefault(goal.LinkedAccountId.Value);
        }

        var effectiveAmount = linkedAccountBalance ?? goal.CurrentAmount;

        // Auto-complete: if effective amount >= TargetAmount after update
        if (goal.Status == GoalStatus.Active && effectiveAmount >= goal.TargetAmount)
        {
            goal.MarkCompleted();
        }

        var updatedGoal = await _goalRepository.UpdateGoalAsync(goal, cancellationToken);
        return MapToDetailDto(updatedGoal, linkedAccountBalance);
    }

    private static DateTime EnsureUtc(DateTime dateTime) => dateTime.Kind switch
    {
        DateTimeKind.Utc => dateTime,
        DateTimeKind.Local => dateTime.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
        _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
    };

    private static GoalDetailDto MapToDetailDto(Goal goal, decimal? linkedAccountBalance)
    {
        var now = DateTimeProvider.UtcNow;
        int? daysRemaining = null;
        if (goal.Deadline.HasValue && goal.Deadline.Value > now)
        {
            daysRemaining = (int)(goal.Deadline.Value.Date - now.Date).TotalDays;
        }

        var currentAmount = linkedAccountBalance ?? goal.CurrentAmount;
        var progressPercentage = goal.TargetAmount > 0
            ? Math.Round((currentAmount / goal.TargetAmount) * 100, 2)
            : 0;
        var remainingAmount = Math.Max(goal.TargetAmount - currentAmount, 0);

        return new GoalDetailDto
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
            LinkedAccountId = goal.LinkedAccountId,
            DisplayOrder = goal.DisplayOrder,
            CreatedAt = goal.CreatedAt,
            UpdatedAt = goal.UpdatedAt
        };
    }
}
