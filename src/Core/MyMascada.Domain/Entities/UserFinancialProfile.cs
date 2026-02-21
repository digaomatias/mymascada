using MyMascada.Domain.Common;

namespace MyMascada.Domain.Entities;

public class UserFinancialProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public decimal MonthlyIncome { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public decimal MonthlyAvailable => MonthlyIncome - MonthlyExpenses;
    public bool OnboardingCompleted { get; set; }
    public DateTime? OnboardingCompletedAt { get; set; }
    public string? DataEntryMethod { get; set; }
}
