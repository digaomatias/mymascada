using System.ComponentModel.DataAnnotations;
using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents a user-created recurring transaction (scheduled bill).
/// Unlike <see cref="RecurringPattern"/> (heuristic detection from bank data),
/// this entity is explicitly created and managed by the user.
/// </summary>
public class RecurringTransaction : BaseEntity
{
    /// <summary>
    /// Maximum number of schedule advancements processed in a single catch-up loop.
    /// Guards against runaway loops (366 covers a full year of daily occurrences).
    /// </summary>
    public const int MaxCatchUpIterations = 366;

    /// <summary>
    /// User ID who owns this recurring transaction
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Account the transaction is scheduled against
    /// </summary>
    [Required]
    public int AccountId { get; set; }

    /// <summary>
    /// Optional category applied to auto-created transactions
    /// </summary>
    public int? CategoryId { get; set; }

    /// <summary>
    /// Description used for reminders and auto-created transactions
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Amount of the scheduled bill (always stored as a positive value).
    /// Auto-created transactions are negated (expenses are negative).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// How often the transaction repeats
    /// </summary>
    [Required]
    public RecurrenceFrequency Frequency { get; set; }

    /// <summary>
    /// Interval in days when Frequency is Custom (1..366)
    /// </summary>
    public int? CustomIntervalDays { get; set; }

    /// <summary>
    /// First scheduled date. Also anchors the day-of-month/day-of-year
    /// for Monthly/Yearly clamping (e.g. due on the 31st → Feb 28 → Mar 31).
    /// </summary>
    [Required]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Optional last date on which an occurrence may fall
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Next date this recurring transaction is due
    /// </summary>
    [Required]
    public DateTime NextDueDate { get; set; }

    /// <summary>
    /// When true the job creates the real transaction automatically;
    /// when false (default) the user only receives a reminder notification.
    /// </summary>
    public bool AutoCreate { get; set; }

    /// <summary>
    /// Whether the schedule is active (false = paused or finished)
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional user notes
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation properties

    /// <summary>
    /// Account the transaction is scheduled against
    /// </summary>
    public Account Account { get; set; } = null!;

    /// <summary>
    /// Category applied to auto-created transactions (if any)
    /// </summary>
    public Category? Category { get; set; }

    /// <summary>
    /// Historical occurrences of this recurring transaction
    /// </summary>
    public ICollection<RecurringTransactionOccurrence> Occurrences { get; set; } = new List<RecurringTransactionOccurrence>();

    // Business logic methods

    /// <summary>
    /// Calculates the occurrence date that follows <paramref name="currentDueDate"/>.
    /// Monthly/Yearly use calendar math with end-of-month clamping anchored on
    /// StartDate's day (e.g. anchor 31st: Jan 31 → Feb 28 → Mar 31).
    /// </summary>
    public DateTime CalculateNextDueDate(DateTime currentDueDate)
    {
        return Frequency switch
        {
            RecurrenceFrequency.Weekly => currentDueDate.AddDays(7),
            RecurrenceFrequency.Fortnightly => currentDueDate.AddDays(14),
            RecurrenceFrequency.Monthly => AddMonthsClamped(currentDueDate, 1, StartDate.Day),
            RecurrenceFrequency.Yearly => AddYearsClamped(currentDueDate, 1, StartDate.Day),
            RecurrenceFrequency.Custom => currentDueDate.AddDays(GetCustomIntervalDays()),
            _ => throw new InvalidOperationException($"Unsupported recurrence frequency: {Frequency}")
        };
    }

    /// <summary>
    /// Advances NextDueDate until it is strictly after <paramref name="today"/>.
    /// Deactivates the schedule when the next due date falls past EndDate.
    /// </summary>
    public void AdvanceNextDueDate(DateTime today)
    {
        var iterations = 0;
        while (NextDueDate.Date <= today.Date && iterations < MaxCatchUpIterations)
        {
            NextDueDate = CalculateNextDueDate(NextDueDate);
            iterations++;
        }

        if (EndDate.HasValue && NextDueDate.Date > EndDate.Value.Date)
        {
            IsActive = false;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Returns the scheduled dates that are due on or before <paramref name="today"/>
    /// (and on or before EndDate, when set), starting from NextDueDate.
    /// </summary>
    public IReadOnlyList<DateTime> GetDueDates(DateTime today)
    {
        var dates = new List<DateTime>();
        var current = NextDueDate.Date;
        var iterations = 0;

        while (current <= today.Date && iterations < MaxCatchUpIterations)
        {
            if (EndDate.HasValue && current > EndDate.Value.Date)
            {
                break;
            }

            dates.Add(current);
            current = CalculateNextDueDate(current).Date;
            iterations++;
        }

        return dates;
    }

    /// <summary>
    /// Returns the scheduled dates falling within [fromDate, toDate], capped at EndDate.
    /// Used to materialize the upcoming schedule for display.
    /// </summary>
    public IReadOnlyList<DateTime> GetScheduledDates(DateTime fromDate, DateTime toDate)
    {
        var dates = new List<DateTime>();
        var current = NextDueDate.Date;
        var iterations = 0;

        while (current <= toDate.Date && iterations < MaxCatchUpIterations)
        {
            if (EndDate.HasValue && current > EndDate.Value.Date)
            {
                break;
            }

            if (current >= fromDate.Date)
            {
                dates.Add(current);
            }

            current = CalculateNextDueDate(current).Date;
            iterations++;
        }

        return dates;
    }

    /// <summary>
    /// Pauses the schedule (user action)
    /// </summary>
    public void Pause()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Resumes the schedule, fast-forwarding NextDueDate past today so
    /// occurrences missed while paused are not retroactively fired.
    /// </summary>
    public void Resume(DateTime today)
    {
        IsActive = true;

        if (NextDueDate.Date <= today.Date)
        {
            AdvanceNextDueDate(today);
            // AdvanceNextDueDate may deactivate when past EndDate — keep that result.
        }
        else
        {
            UpdatedAt = DateTime.UtcNow;
        }
    }

    private int GetCustomIntervalDays()
    {
        if (!CustomIntervalDays.HasValue || CustomIntervalDays.Value < 1)
        {
            throw new InvalidOperationException(
                "CustomIntervalDays must be set to a positive value when Frequency is Custom.");
        }

        return CustomIntervalDays.Value;
    }

    /// <summary>
    /// Adds calendar months preserving the anchor day-of-month, clamped to the
    /// target month's length (anchor 31 → Feb 28/29, Apr 30, May 31, ...).
    /// </summary>
    internal static DateTime AddMonthsClamped(DateTime date, int months, int anchorDay)
    {
        var target = date.AddMonths(months);
        var day = Math.Min(anchorDay, DateTime.DaysInMonth(target.Year, target.Month));
        return new DateTime(target.Year, target.Month, day, 0, 0, 0, date.Kind);
    }

    /// <summary>
    /// Adds calendar years preserving the anchor day-of-month, clamped for
    /// non-leap years (Feb 29 → Feb 28, back to Feb 29 on the next leap year).
    /// </summary>
    internal static DateTime AddYearsClamped(DateTime date, int years, int anchorDay)
    {
        var targetYear = date.Year + years;
        var day = Math.Min(anchorDay, DateTime.DaysInMonth(targetYear, date.Month));
        return new DateTime(targetYear, date.Month, day, 0, 0, 0, date.Kind);
    }
}
