using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

public class Goal : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public decimal TargetAmount { get; set; }

    public decimal CurrentAmount { get; set; }

    public DateTime? Deadline { get; set; }

    public GoalType GoalType { get; set; }

    public GoalStatus Status { get; set; } = GoalStatus.Active;

    public int? LinkedAccountId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public int DisplayOrder { get; set; }

    // Navigation properties
    public Account? Account { get; set; }

    /// <summary>
    /// Gets the progress percentage (0-100)
    /// </summary>
    public decimal GetProgressPercentage()
    {
        if (TargetAmount <= 0)
            return 0m;

        var percentage = CurrentAmount / TargetAmount * 100;
        return Math.Clamp(Math.Round(percentage, 1), 0m, 100m);
    }

    /// <summary>
    /// Gets the remaining amount to reach the target
    /// </summary>
    public decimal GetRemainingAmount()
    {
        var remaining = TargetAmount - CurrentAmount;
        return remaining < 0 ? 0 : remaining;
    }

    /// <summary>
    /// Checks if the goal is on track to meet the deadline based on current pace
    /// </summary>
    public bool IsOnTrack()
    {
        if (!Deadline.HasValue)
            return true; // No deadline means always on track

        if (Status == GoalStatus.Completed)
            return true;

        var now = DateTimeProvider.UtcNow;
        if (now >= Deadline.Value)
            return CurrentAmount >= TargetAmount;

        var totalDays = (Deadline.Value - CreatedAt).TotalDays;
        if (totalDays <= 0)
            return CurrentAmount >= TargetAmount;

        var elapsedDays = (now - CreatedAt).TotalDays;
        var expectedProgress = (decimal)(elapsedDays / totalDays);
        var expectedAmount = TargetAmount * expectedProgress;

        return CurrentAmount >= expectedAmount;
    }

    /// <summary>
    /// Marks the goal as completed
    /// </summary>
    public void MarkCompleted()
    {
        Status = GoalStatus.Completed;
        CurrentAmount = TargetAmount;
        UpdatedAt = DateTimeProvider.UtcNow;
    }

    /// <summary>
    /// Marks the goal as paused
    /// </summary>
    public void MarkPaused()
    {
        Status = GoalStatus.Paused;
        UpdatedAt = DateTimeProvider.UtcNow;
    }

    /// <summary>
    /// Marks the goal as abandoned
    /// </summary>
    public void MarkAbandoned()
    {
        Status = GoalStatus.Abandoned;
        UpdatedAt = DateTimeProvider.UtcNow;
    }
}
