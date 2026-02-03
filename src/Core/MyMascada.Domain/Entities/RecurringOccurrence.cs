using System.ComponentModel.DataAnnotations;
using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents a single occurrence (expected or actual) of a recurring pattern.
/// Used to track payment history and detect missed payments.
/// </summary>
public class RecurringOccurrence : BaseEntity
{
    /// <summary>
    /// Foreign key to the parent recurring pattern
    /// </summary>
    [Required]
    public int PatternId { get; set; }

    /// <summary>
    /// Optional foreign key to the matched transaction
    /// Null if the occurrence was missed (no matching transaction found)
    /// </summary>
    public int? TransactionId { get; set; }

    /// <summary>
    /// The date when this occurrence was expected
    /// </summary>
    [Required]
    public DateTime ExpectedDate { get; set; }

    /// <summary>
    /// The date when a matching transaction was actually posted (if any)
    /// </summary>
    public DateTime? ActualDate { get; set; }

    /// <summary>
    /// The outcome of this occurrence (Posted, Missed, Late)
    /// </summary>
    [Required]
    public OccurrenceOutcome Outcome { get; set; } = OccurrenceOutcome.Posted;

    /// <summary>
    /// The actual amount of the matched transaction (if any)
    /// </summary>
    public decimal? ActualAmount { get; set; }

    /// <summary>
    /// The expected amount based on the pattern's average
    /// </summary>
    public decimal ExpectedAmount { get; set; }

    /// <summary>
    /// Notes or remarks about this occurrence
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation properties

    /// <summary>
    /// Parent recurring pattern
    /// </summary>
    public RecurringPattern Pattern { get; set; } = null!;

    /// <summary>
    /// Matched transaction (if Posted or Late)
    /// </summary>
    public Transaction? Transaction { get; set; }

    // Business logic methods

    /// <summary>
    /// Checks if this occurrence was on time (Posted outcome)
    /// </summary>
    public bool WasOnTime => Outcome == OccurrenceOutcome.Posted;

    /// <summary>
    /// Checks if this occurrence was missed (no matching transaction)
    /// </summary>
    public bool WasMissed => Outcome == OccurrenceOutcome.Missed;

    /// <summary>
    /// Checks if this occurrence was late (posted after expected date but within grace window)
    /// </summary>
    public bool WasLate => Outcome == OccurrenceOutcome.Late;

    /// <summary>
    /// Gets the amount variance (actual vs expected) if applicable
    /// </summary>
    public decimal? GetAmountVariance()
    {
        if (!ActualAmount.HasValue || ExpectedAmount == 0)
            return null;

        return ActualAmount.Value - ExpectedAmount;
    }

    /// <summary>
    /// Gets the amount variance as a percentage if applicable
    /// </summary>
    public decimal? GetAmountVariancePercentage()
    {
        if (!ActualAmount.HasValue || ExpectedAmount == 0)
            return null;

        return Math.Round((ActualAmount.Value - ExpectedAmount) / ExpectedAmount * 100, 2);
    }

    /// <summary>
    /// Gets the number of days between expected and actual date (positive = late, negative = early)
    /// </summary>
    public int? GetDaysLate()
    {
        if (!ActualDate.HasValue)
            return null;

        return (int)(ActualDate.Value.Date - ExpectedDate.Date).TotalDays;
    }

    /// <summary>
    /// Creates a new occurrence for a missed payment
    /// </summary>
    public static RecurringOccurrence CreateMissed(int patternId, DateTime expectedDate, decimal expectedAmount)
    {
        return new RecurringOccurrence
        {
            PatternId = patternId,
            ExpectedDate = expectedDate,
            ExpectedAmount = expectedAmount,
            Outcome = OccurrenceOutcome.Missed,
            TransactionId = null,
            ActualDate = null,
            ActualAmount = null
        };
    }

    /// <summary>
    /// Creates a new occurrence for a posted payment
    /// </summary>
    public static RecurringOccurrence CreatePosted(
        int patternId,
        DateTime expectedDate,
        decimal expectedAmount,
        int transactionId,
        DateTime actualDate,
        decimal actualAmount)
    {
        var outcome = actualDate.Date > expectedDate.Date
            ? OccurrenceOutcome.Late
            : OccurrenceOutcome.Posted;

        return new RecurringOccurrence
        {
            PatternId = patternId,
            ExpectedDate = expectedDate,
            ExpectedAmount = expectedAmount,
            TransactionId = transactionId,
            ActualDate = actualDate,
            ActualAmount = actualAmount,
            Outcome = outcome
        };
    }
}
