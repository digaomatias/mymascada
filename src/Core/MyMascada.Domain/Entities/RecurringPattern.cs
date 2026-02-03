using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents a detected recurring payment pattern (subscription or bill).
/// Tracks expected payment dates, amounts, and status for proactive notifications.
/// </summary>
public class RecurringPattern : BaseEntity
{
    /// <summary>
    /// User ID who owns this recurring pattern
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Display name for the merchant (formatted for user display)
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string MerchantName { get; set; } = string.Empty;

    /// <summary>
    /// Normalized merchant key for matching transactions (lowercase, cleaned)
    /// Used for Levenshtein distance comparison
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string NormalizedMerchantKey { get; set; } = string.Empty;

    /// <summary>
    /// Detected interval between payments in days (7=weekly, 14=biweekly, 30=monthly)
    /// </summary>
    public int IntervalDays { get; set; }

    /// <summary>
    /// Average amount of the recurring payment (stored as positive value)
    /// </summary>
    public decimal AverageAmount { get; set; }

    /// <summary>
    /// Confidence score for the pattern detection (0.0 to 1.0)
    /// Based on occurrence count, interval consistency, and amount consistency
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// Current status of the recurring pattern
    /// </summary>
    public RecurringPatternStatus Status { get; set; } = RecurringPatternStatus.Active;

    /// <summary>
    /// Next expected date for this recurring payment
    /// </summary>
    public DateTime NextExpectedDate { get; set; }

    /// <summary>
    /// Last observed transaction date for this pattern
    /// </summary>
    public DateTime LastObservedAt { get; set; }

    /// <summary>
    /// Number of consecutive missed payments
    /// Reset to 0 when a matching transaction is found
    /// </summary>
    public int ConsecutiveMisses { get; set; }

    /// <summary>
    /// Optional category ID for budget integration
    /// </summary>
    public int? CategoryId { get; set; }

    /// <summary>
    /// Total number of occurrences detected for this pattern
    /// </summary>
    public int OccurrenceCount { get; set; }

    /// <summary>
    /// User-provided notes or description override
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation properties

    /// <summary>
    /// Associated category (if assigned)
    /// </summary>
    public Category? Category { get; set; }

    /// <summary>
    /// Historical occurrences of this recurring pattern
    /// </summary>
    public ICollection<RecurringOccurrence> Occurrences { get; set; } = new List<RecurringOccurrence>();

    // Business logic methods

    /// <summary>
    /// Calculates the grace window end date for the next expected payment.
    /// Grace window = NextExpectedDate + (IntervalDays * 1.3)
    /// </summary>
    public DateTime GetGraceWindowEnd()
    {
        return NextExpectedDate.AddDays(IntervalDays * 0.3);
    }

    /// <summary>
    /// Checks if the expected payment is within the grace window
    /// </summary>
    public bool IsWithinGraceWindow(DateTime currentDate)
    {
        return currentDate <= GetGraceWindowEnd();
    }

    /// <summary>
    /// Checks if the pattern should be marked as at risk (first miss)
    /// </summary>
    public bool ShouldMarkAtRisk(DateTime currentDate)
    {
        return Status == RecurringPatternStatus.Active
               && !IsWithinGraceWindow(currentDate)
               && ConsecutiveMisses == 0;
    }

    /// <summary>
    /// Checks if the pattern should be marked as cancelled (second consecutive miss)
    /// </summary>
    public bool ShouldMarkCancelled(DateTime currentDate)
    {
        return Status == RecurringPatternStatus.AtRisk
               && !IsWithinGraceWindow(currentDate)
               && ConsecutiveMisses >= 1;
    }

    /// <summary>
    /// Records a missed payment and updates status accordingly
    /// </summary>
    public void RecordMiss(DateTime missDate)
    {
        ConsecutiveMisses++;
        UpdatedAt = DateTime.UtcNow;

        if (ConsecutiveMisses >= 2)
        {
            Status = RecurringPatternStatus.Cancelled;
        }
        else if (ConsecutiveMisses == 1)
        {
            Status = RecurringPatternStatus.AtRisk;
        }
    }

    /// <summary>
    /// Records a successful payment match and resets the miss counter
    /// </summary>
    public void RecordMatch(DateTime matchDate, decimal amount)
    {
        ConsecutiveMisses = 0;
        Status = RecurringPatternStatus.Active;
        LastObservedAt = matchDate;
        OccurrenceCount++;

        // Update average amount with rolling average
        AverageAmount = ((AverageAmount * (OccurrenceCount - 1)) + Math.Abs(amount)) / OccurrenceCount;

        // Calculate next expected date
        NextExpectedDate = matchDate.AddDays(IntervalDays);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Pauses the pattern (user action)
    /// </summary>
    public void Pause()
    {
        Status = RecurringPatternStatus.Paused;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Resumes the pattern (user action)
    /// </summary>
    public void Resume()
    {
        Status = RecurringPatternStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Cancels the pattern (user action)
    /// </summary>
    public void Cancel()
    {
        Status = RecurringPatternStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the interval name for display (Weekly/Biweekly/Monthly)
    /// </summary>
    public string GetIntervalName()
    {
        return IntervalDays switch
        {
            <= 9 => "Weekly",
            <= 16 => "Biweekly",
            <= 35 => "Monthly",
            _ => $"Every {IntervalDays} days"
        };
    }

    /// <summary>
    /// Gets confidence level as string (High/Medium/Low)
    /// </summary>
    public string GetConfidenceLevel()
    {
        return Confidence switch
        {
            >= 0.75m => "High",
            >= 0.5m => "Medium",
            _ => "Low"
        };
    }

    /// <summary>
    /// Calculates the estimated annual cost based on average amount and interval
    /// </summary>
    public decimal GetAnnualCost()
    {
        if (IntervalDays <= 0) return 0;
        return Math.Round(AverageAmount * (365m / IntervalDays), 2);
    }

    /// <summary>
    /// Calculates the estimated monthly cost based on average amount and interval
    /// </summary>
    public decimal GetMonthlyCost()
    {
        if (IntervalDays <= 0) return 0;
        return Math.Round(AverageAmount * (30.44m / IntervalDays), 2);
    }

    /// <summary>
    /// Gets the number of days until the next expected payment
    /// </summary>
    public int GetDaysUntilDue(DateTime fromDate)
    {
        return (int)(NextExpectedDate - fromDate.Date).TotalDays;
    }

    /// <summary>
    /// Checks if a transaction matches this pattern (description similarity)
    /// </summary>
    public bool MatchesTransaction(string description, decimal amount, decimal similarityThreshold = 0.8m)
    {
        if (string.IsNullOrWhiteSpace(description))
            return false;

        var normalizedDescription = NormalizeDescription(description);
        var similarity = CalculateStringSimilarity(NormalizedMerchantKey, normalizedDescription);

        // Check description similarity and amount range (±20%)
        var amountRangeMatch = Math.Abs(amount) >= AverageAmount * 0.8m
                               && Math.Abs(amount) <= AverageAmount * 1.2m;

        return similarity >= similarityThreshold && amountRangeMatch;
    }

    /// <summary>
    /// Normalizes a transaction description for matching
    /// </summary>
    public static string NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return string.Empty;

        // Convert to lowercase and remove extra whitespace
        var normalized = Regex.Replace(description.ToLowerInvariant().Trim(), @"\s+", " ");

        // Remove common transaction prefixes/suffixes
        normalized = Regex.Replace(normalized, @"^(purchase\s+|payment\s+|pos\s+|debit\s+|eftpos\s+)", "");

        // Remove reference numbers (patterns like #123, REF:ABC123, etc.)
        normalized = Regex.Replace(normalized, @"(#|ref:?|id:?)\s*[\w\d-]+", "");

        // Remove dates (patterns like 01/15, 15-Jan, etc.)
        normalized = Regex.Replace(normalized, @"\d{1,2}[/-]\d{1,2}([/-]\d{2,4})?", "");

        // Remove time patterns
        normalized = Regex.Replace(normalized, @"\d{1,2}:\d{2}(:\d{2})?(\s*(am|pm))?", "");

        // Remove trailing numbers that might be transaction IDs
        normalized = Regex.Replace(normalized, @"\s+\d+$", "");

        // Clean up extra whitespace again
        return Regex.Replace(normalized.Trim(), @"\s+", " ");
    }

    private static decimal CalculateStringSimilarity(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) && string.IsNullOrEmpty(str2))
            return 1m;

        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
            return 0m;

        var distance = LevenshteinDistance(str1, str2);
        var maxLength = Math.Max(str1.Length, str2.Length);

        return 1m - (decimal)distance / maxLength;
    }

    private static int LevenshteinDistance(string s1, string s2)
    {
        if (s1.Length == 0) return s2.Length;
        if (s2.Length == 0) return s1.Length;

        var matrix = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
            matrix[i, 0] = i;

        for (int j = 0; j <= s2.Length; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                var cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[s1.Length, s2.Length];
    }
}
