namespace MyMascada.WebAPI.Helpers;

/// <summary>
/// Date and time utility methods
/// </summary>
public static class DateUtils
{
    /// <summary>
    /// Returns a human-friendly relative time string (e.g., "2 hours ago").
    /// Expects UTC input; compares against <see cref="DateTime.UtcNow"/>.
    /// </summary>
    public static string ToRelativeTime(DateTime dateTime)
    {
        var span = DateTime.UtcNow - dateTime;

        if (span < TimeSpan.Zero)
            return "in the future";

        if (span.TotalMinutes < 1) return "just now";

        var minutes = (int)span.TotalMinutes;
        if (minutes < 60) return minutes == 1 ? "1 minute ago" : $"{minutes} minutes ago";

        var hours = (int)span.TotalHours;
        if (hours < 24) return hours == 1 ? "1 hour ago" : $"{hours} hours ago";

        var days = (int)span.TotalDays;
        if (days < 30) return days == 1 ? "1 day ago" : $"{days} days ago";

        if (days < 365)
        {
            var months = days / 30;
            return months == 1 ? "1 month ago" : $"{months} months ago";
        }

        var years = days / 365;
        return years == 1 ? "1 year ago" : $"{years} years ago";
    }

    /// <summary>
    /// Checks if a date falls on a weekday (Mon–Fri).
    /// Does not account for public holidays; use in conjunction with a holiday
    /// calendar for full business-day logic.
    /// </summary>
    public static bool IsWeekday(DateTime date)
    {
        return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;
    }

    /// <summary>
    /// Checks if a date falls on a business day, excluding weekends and
    /// any provided public holidays.
    /// </summary>
    public static bool IsBusinessDay(DateTime date, IEnumerable<DateTime>? holidays = null)
    {
        if (!IsWeekday(date))
            return false;

        return holidays?.Any(h => h.Date == date.Date) != true;
    }

    /// <summary>
    /// Gets the start of the NZ financial year (April 1) that the given date falls within.
    /// Dates in Jan–Mar belong to the financial year that started the previous April.
    /// </summary>
    public static DateTime GetFinancialYearStart(DateTime date)
    {
        return date.Month >= 4
            ? new DateTime(date.Year, 4, 1)
            : new DateTime(date.Year - 1, 4, 1);
    }
}
