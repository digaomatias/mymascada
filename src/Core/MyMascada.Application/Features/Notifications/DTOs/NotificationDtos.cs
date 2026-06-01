namespace MyMascada.Application.Features.Notifications.DTOs;

public class NotificationDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Data { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

public class NotificationListResponse
{
    public List<NotificationDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class UnreadCountResponse
{
    public int Count { get; set; }
}

public class NotificationPreferenceDto
{
    public string? ChannelPreferences { get; set; }
    public string? QuietHoursStart { get; set; }
    public string? QuietHoursEnd { get; set; }
    public string? QuietHoursTimezone { get; set; }
    public decimal? LargeTransactionThreshold { get; set; }
    public int? BudgetAlertPercentage { get; set; }
    public int? RunwayWarningMonths { get; set; }
}

public class UpdateNotificationPreferenceRequest
{
    public string? ChannelPreferences { get; set; }
    public string? QuietHoursStart { get; set; }
    public string? QuietHoursEnd { get; set; }
    public string? QuietHoursTimezone { get; set; }
    public decimal? LargeTransactionThreshold { get; set; }
    public int? BudgetAlertPercentage { get; set; }
    public int? RunwayWarningMonths { get; set; }
}
