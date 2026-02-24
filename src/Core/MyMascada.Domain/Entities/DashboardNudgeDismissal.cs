namespace MyMascada.Domain.Entities;

public class DashboardNudgeDismissal : Common.BaseEntity
{
    public Guid UserId { get; set; }
    public string NudgeType { get; set; } = string.Empty;
    public DateTime SnoozedUntil { get; set; }
}
