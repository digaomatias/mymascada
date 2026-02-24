using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Goals.DTOs;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Goals.Commands;

public class CreateGoalCommand : IRequest<GoalDetailDto>
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TargetAmount { get; set; }
    public DateTime? Deadline { get; set; }
    public string GoalType { get; set; } = "Savings";
    public int? LinkedAccountId { get; set; }
    public Guid UserId { get; set; }
}

public class CreateGoalCommandHandler : IRequestHandler<CreateGoalCommand, GoalDetailDto>
{
    private readonly IGoalRepository _goalRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;

    public CreateGoalCommandHandler(
        IGoalRepository goalRepository,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository)
    {
        _goalRepository = goalRepository;
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
    }

    public async Task<GoalDetailDto> Handle(CreateGoalCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Goal name is required.");
        }

        if (request.TargetAmount <= 0)
        {
            throw new ArgumentException("Target amount must be greater than zero.");
        }

        if (!Enum.TryParse<GoalType>(request.GoalType, true, out var goalType))
        {
            throw new ArgumentException($"Invalid goal type: {request.GoalType}. Valid values are: EmergencyFund, Savings, DebtPayoff, Investment, Custom");
        }

        // Validate linked account belongs to user
        if (request.LinkedAccountId.HasValue)
        {
            var account = await _accountRepository.GetByIdAsync(request.LinkedAccountId.Value, request.UserId);
            if (account == null)
            {
                throw new ArgumentException("Linked account not found or you don't have permission to access it.");
            }
        }

        // Check for duplicate name
        if (await _goalRepository.GoalNameExistsAsync(request.UserId, request.Name.Trim(), cancellationToken: cancellationToken))
        {
            throw new ArgumentException($"A goal with the name '{request.Name.Trim()}' already exists.");
        }

        var goal = new Goal
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            TargetAmount = request.TargetAmount,
            CurrentAmount = 0,
            Deadline = request.Deadline.HasValue ? EnsureUtc(request.Deadline.Value) : null,
            GoalType = goalType,
            Status = GoalStatus.Active,
            LinkedAccountId = request.LinkedAccountId,
            UserId = request.UserId,
            DisplayOrder = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createdGoal = await _goalRepository.CreateGoalAsync(goal, cancellationToken);

        // Look up live account balance for linked goals
        decimal? linkedAccountBalance = null;
        if (createdGoal.LinkedAccountId.HasValue)
        {
            var accountBalances = await _transactionRepository.GetAccountBalancesAsync(request.UserId);
            linkedAccountBalance = accountBalances.GetValueOrDefault(createdGoal.LinkedAccountId.Value);
        }

        return MapToDetailDto(createdGoal, linkedAccountBalance);
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
            IsPinned = goal.IsPinned,
            CreatedAt = goal.CreatedAt,
            UpdatedAt = goal.UpdatedAt
        };
    }
}
