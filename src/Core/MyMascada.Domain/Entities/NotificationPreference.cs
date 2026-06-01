using MyMascada.Domain.Common;

namespace MyMascada.Domain.Entities;

/// <summary>
/// User preferences for notification delivery.
/// Stores per-type channel toggles and global settings like quiet hours and thresholds.
/// </summary>
public class NotificationPreference : BaseEntity<Guid>
{
    public Guid UserId { get; set; }

    /// <summary>
    /// JSON object with per-type channel toggles.
    /// Structure: { "TransactionReminder": { "inApp": true, "push": true, "email": false }, ... }
    /// </summary>
    public string? ChannelPreferences { get; set; }

    // Quiet hours
    public TimeOnly? QuietHoursStart { get; set; }
    public TimeOnly? QuietHoursEnd { get; set; }
    public string? QuietHoursTimezone { get; set; }

    // Custom thresholds
    public decimal? LargeTransactionThreshold { get; set; }
    public int? BudgetAlertPercentage { get; set; }
    public int? RunwayWarningMonths { get; set; }
}
