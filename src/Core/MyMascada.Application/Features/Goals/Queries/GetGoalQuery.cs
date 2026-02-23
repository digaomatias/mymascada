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
    private readonly ITransactionRepository _transactionRepository;

    public GetGoalQueryHandler(IGoalRepository goalRepository, ITransactionRepository transactionRepository)
    {
        _goalRepository = goalRepository;
        _transactionRepository = transactionRepository;
    }

    public async Task<GoalDetailDto?> Handle(GetGoalQuery request, CancellationToken cancellationToken)
    {
        var goal = await _goalRepository.GetGoalByIdAsync(request.GoalId, request.UserId, cancellationToken);
        if (goal == null)
        {
            return null;
        }

        // Look up live account balance for goals with linked accounts
        decimal? linkedAccountBalance = null;
        if (goal.LinkedAccountId.HasValue)
        {
            var accountBalances = await _transactionRepository.GetAccountBalancesAsync(request.UserId);
            linkedAccountBalance = accountBalances.GetValueOrDefault(goal.LinkedAccountId.Value);
        }

        return MapToDetailDto(goal, linkedAccountBalance);
    }

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
