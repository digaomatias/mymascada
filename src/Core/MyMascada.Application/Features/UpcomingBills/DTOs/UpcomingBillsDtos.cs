namespace MyMascada.Application.Features.UpcomingBills.DTOs;

/// <summary>
/// Response containing upcoming bills detected from transaction patterns
/// </summary>
public class UpcomingBillsResponse
{
    public List<UpcomingBillDto> Bills { get; set; } = new();
    public int TotalBillsCount { get; set; }
    public decimal TotalExpectedAmount { get; set; }
}

/// <summary>
/// A single upcoming bill with predicted details
/// </summary>
public class UpcomingBillDto
{
    /// <summary>
    /// Pattern ID (if from persisted data, null if from on-demand calculation)
    /// </summary>
    public int? PatternId { get; set; }
    public string MerchantName { get; set; } = string.Empty;
    public decimal ExpectedAmount { get; set; }
    public DateTime ExpectedDate { get; set; }
    public int DaysUntilDue { get; set; }
    public decimal ConfidenceScore { get; set; } // 0.0-1.0
    public string ConfidenceLevel { get; set; } = string.Empty; // "High" or "Medium"
    public string Interval { get; set; } = string.Empty; // Weekly/Biweekly/Monthly
    public int OccurrenceCount { get; set; }
}

/// <summary>
/// Represents detected recurrence intervals
/// </summary>
public enum RecurrenceInterval
{
    Weekly = 7,
    Biweekly = 14,
    Monthly = 30
}
