namespace MyMascada.Application.Features.Onboarding.DTOs;

public class CompleteOnboardingRequest
{
    public decimal MonthlyIncome { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public string GoalName { get; set; } = string.Empty;
    public decimal GoalTargetAmount { get; set; }
    public string GoalType { get; set; } = "EmergencyFund";
    public string DataEntryMethod { get; set; } = "manual";
    public int? LinkedAccountId { get; set; }
}

public class OnboardingCompleteResponse
{
    public int ProfileId { get; set; }
    public int GoalId { get; set; }
    public decimal MonthlyIncome { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public decimal MonthlyAvailable { get; set; }
}

public class OnboardingStatusResponse
{
    public bool IsComplete { get; set; }
    public decimal? MonthlyIncome { get; set; }
    public decimal? MonthlyExpenses { get; set; }
    public decimal? MonthlyAvailable { get; set; }
}
