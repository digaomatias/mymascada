namespace MyMascada.Application.Features.Goals.DTOs;

public class CoachingInsightDto
{
    public string InsightKey { get; set; } = string.Empty;
    public Dictionary<string, string>? InsightParams { get; set; }
    public string InsightIcon { get; set; } = string.Empty;
    public string NudgeTone { get; set; } = string.Empty;
    public string NudgeKey { get; set; } = string.Empty;
    public int? NudgeTargetGoalId { get; set; }
}
