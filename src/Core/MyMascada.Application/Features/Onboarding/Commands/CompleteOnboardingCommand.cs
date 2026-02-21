using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Onboarding.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Onboarding.Commands;

public class CompleteOnboardingCommand : IRequest<OnboardingCompleteResponse>
{
    public Guid UserId { get; set; }
    public decimal MonthlyIncome { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public string GoalName { get; set; } = string.Empty;
    public decimal GoalTargetAmount { get; set; }
    public string GoalType { get; set; } = "EmergencyFund";
    public string DataEntryMethod { get; set; } = "manual";
}

public class CompleteOnboardingCommandHandler : IRequestHandler<CompleteOnboardingCommand, OnboardingCompleteResponse>
{
    private readonly IUserFinancialProfileRepository _profileRepository;
    private readonly IGoalRepository _goalRepository;

    public CompleteOnboardingCommandHandler(
        IUserFinancialProfileRepository profileRepository,
        IGoalRepository goalRepository)
    {
        _profileRepository = profileRepository;
        _goalRepository = goalRepository;
    }

    public async Task<OnboardingCompleteResponse> Handle(CompleteOnboardingCommand request, CancellationToken cancellationToken)
    {
        if (request.MonthlyIncome < 0)
        {
            throw new ArgumentException("Monthly income cannot be negative.");
        }

        if (request.MonthlyExpenses < 0)
        {
            throw new ArgumentException("Monthly expenses cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(request.GoalName))
        {
            throw new ArgumentException("Goal name is required.");
        }

        if (request.GoalTargetAmount <= 0)
        {
            throw new ArgumentException("Goal target amount must be greater than zero.");
        }

        if (!Enum.TryParse<GoalType>(request.GoalType, true, out var goalType))
        {
            throw new ArgumentException($"Invalid goal type: {request.GoalType}.");
        }

        // Check if profile already exists (idempotency)
        var existingProfile = await _profileRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (existingProfile != null && existingProfile.OnboardingCompleted)
        {
            throw new InvalidOperationException("Onboarding has already been completed.");
        }

        // Create or update financial profile
        UserFinancialProfile profile;
        if (existingProfile != null)
        {
            existingProfile.MonthlyIncome = request.MonthlyIncome;
            existingProfile.MonthlyExpenses = request.MonthlyExpenses;
            existingProfile.DataEntryMethod = request.DataEntryMethod;
            existingProfile.OnboardingCompleted = true;
            existingProfile.OnboardingCompletedAt = DateTime.UtcNow;
            profile = await _profileRepository.UpdateAsync(existingProfile, cancellationToken);
        }
        else
        {
            profile = new UserFinancialProfile
            {
                UserId = request.UserId,
                MonthlyIncome = request.MonthlyIncome,
                MonthlyExpenses = request.MonthlyExpenses,
                DataEntryMethod = request.DataEntryMethod,
                OnboardingCompleted = true,
                OnboardingCompletedAt = DateTime.UtcNow
            };
            profile = await _profileRepository.CreateAsync(profile, cancellationToken);
        }

        // Create the first goal
        var goal = new Goal
        {
            Name = request.GoalName.Trim(),
            TargetAmount = request.GoalTargetAmount,
            CurrentAmount = 0,
            GoalType = goalType,
            Status = GoalStatus.Active,
            UserId = request.UserId,
            DisplayOrder = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createdGoal = await _goalRepository.CreateGoalAsync(goal, cancellationToken);

        return new OnboardingCompleteResponse
        {
            ProfileId = profile.Id,
            GoalId = createdGoal.Id,
            MonthlyIncome = profile.MonthlyIncome,
            MonthlyExpenses = profile.MonthlyExpenses,
            MonthlyAvailable = profile.MonthlyAvailable
        };
    }
}
