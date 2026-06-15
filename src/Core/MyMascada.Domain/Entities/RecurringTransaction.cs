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
    /// Maximum number of dates materialized by GetDueDates/GetScheduledDates.
    /// Caps how many occurrences can be fired/displayed in one pass
    /// (366 covers a full year of daily occurrences).
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
    /// Advances NextDueDate until it is strictly after <paramref name="today"/>,
    /// no matter how far behind the schedule is (arbitrarily old StartDate,
    /// long pauses, etc.). Deactivates the schedule when the next due date
    /// falls past EndDate.
    /// </summary>
    public void AdvanceNextDueDate(DateTime today)
    {
        if (NextDueDate.Date <= today.Date)
        {
            NextDueDate = FastForward(NextDueDate, today);
        }

        if (EndDate.HasValue && NextDueDate.Date > EndDate.Value.Date)
        {
            IsActive = false;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Advances NextDueDate to the occurrence that follows
    /// <paramref name="lastFiredDate"/> (the last due date a processing run
    /// materialized). Unlike <see cref="AdvanceNextDueDate"/> the result may
    /// still be in the past: when a run hits <see cref="MaxCatchUpIterations"/>
    /// the remaining due dates stay due, so the next run continues catching up
    /// instead of silently skipping them. Deactivates the schedule when the
    /// next due date falls past EndDate.
    /// </summary>
    public void AdvanceNextDueDateAfter(DateTime lastFiredDate)
    {
        NextDueDate = CalculateNextDueDate(lastFiredDate.Date);

        if (EndDate.HasValue && NextDueDate.Date > EndDate.Value.Date)
        {
            IsActive = false;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Returns the first occurrence strictly after <paramref name="today"/>,
    /// starting from <paramref name="from"/>. Fixed-interval frequencies snap
    /// forward arithmetically so a schedule years behind cannot land short of
    /// today; calendar frequencies advance step-by-step to preserve the
    /// anchor-day clamping (each step moves at least 28 days, so the loop
    /// terminates without an iteration cap).
    /// </summary>
    private DateTime FastForward(DateTime from, DateTime today)
    {
        return Frequency switch
        {
            RecurrenceFrequency.Weekly => FastForwardByDays(from, today, 7),
            RecurrenceFrequency.Fortnightly => FastForwardByDays(from, today, 14),
            RecurrenceFrequency.Custom => FastForwardByDays(from, today, GetCustomIntervalDays()),
            _ => FastForwardCalendar(from, today)
        };
    }

    /// <summary>
    /// Snaps a fixed-interval schedule to the first occurrence strictly after
    /// <paramref name="today"/>, preserving the original anchor (date + N * interval).
    /// </summary>
    private static DateTime FastForwardByDays(DateTime from, DateTime today, int intervalDays)
    {
        var daysBehind = (today.Date - from.Date).Days; // >= 0 — caller checked
        var periods = (daysBehind / intervalDays) + 1;
        return from.AddDays((double)periods * intervalDays);
    }

    /// <summary>
    /// Advances a Monthly/Yearly schedule one calendar step at a time until it
    /// is strictly after <paramref name="today"/>.
    /// </summary>
    private DateTime FastForwardCalendar(DateTime from, DateTime today)
    {
        var current = from;
        while (current.Date <= today.Date)
        {
            current = CalculateNextDueDate(current);
        }

        return current;
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

        // A stale NextDueDate (long pause, job downtime) could otherwise burn
        // the whole iteration budget before reaching the window and return an
        // empty list. Jump straight to the first occurrence on/after fromDate,
        // preserving the schedule anchor.
        if (current < fromDate.Date)
        {
            current = FastForward(current, fromDate.Date.AddDays(-1)).Date;
        }

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
