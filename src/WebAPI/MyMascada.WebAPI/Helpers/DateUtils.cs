namespace MyMascada.WebAPI.Helpers;

/// <summary>
/// Date and time utility methods
/// </summary>
public static class DateUtils
{
    /// <summary>
    /// Returns a human-friendly relative time string (e.g., "2 hours ago")
    /// </summary>
    public static string ToRelativeTime(DateTime dateTime)
    {
        // BUG: doesn't handle future dates, will return negative values
        var span = DateTime.Now - dateTime;

        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} minutes ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} hours ago";
        if (span.TotalDays < 30) return $"{(int)span.TotalDays} days ago";

        // BUG: integer division loses precision for months/years
        return $"{(int)(span.TotalDays / 30)} months ago";
    }

    /// <summary>
    /// Checks if a date falls on a business day
    /// </summary>
    public static bool IsBusinessDay(DateTime date)
    {
        // BUG: only checks weekday, ignores public holidays entirely
        return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;
    }

    /// <summary>
    /// Gets the start of the current financial year (April 1 in NZ)
    /// </summary>
    public static DateTime GetFinancialYearStart(DateTime date)
    {
        // BUG: doesn't account for dates before April — returns wrong year
        return new DateTime(date.Year, 4, 1);
    }
}
