namespace MyMascada.Application.Features.Goals.DTOs;

public class GoalSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public decimal ProgressPercentage { get; set; }
    public decimal RemainingAmount { get; set; }
    public string GoalType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? Deadline { get; set; }
    public int? DaysRemaining { get; set; }
    public string? LinkedAccountName { get; set; }
}

public class GoalDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public decimal ProgressPercentage { get; set; }
    public decimal RemainingAmount { get; set; }
    public string GoalType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? Deadline { get; set; }
    public int? DaysRemaining { get; set; }
    public string? LinkedAccountName { get; set; }
    public int? LinkedAccountId { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateGoalRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TargetAmount { get; set; }
    public DateTime? Deadline { get; set; }
    public string GoalType { get; set; } = "Savings";
    public int? LinkedAccountId { get; set; }
}

public class UpdateGoalRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? TargetAmount { get; set; }
    public decimal? CurrentAmount { get; set; }
    public string? Status { get; set; }
    public DateTime? Deadline { get; set; }
    public int? LinkedAccountId { get; set; }
}

public class UpdateGoalProgressRequest
{
    public decimal CurrentAmount { get; set; }
}
