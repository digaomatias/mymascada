using System;

namespace MyMascada.Domain.Common;

/// <summary>
/// Provides consistent DateTime handling across the application
/// Ensures all DateTime values are properly handled for PostgreSQL compatibility
/// </summary>
public static class DateTimeProvider
{
    /// <summary>
    /// Gets the current UTC datetime
    /// </summary>
    public static DateTime UtcNow => DateTime.UtcNow;

    /// <summary>
    /// Converts any DateTime to UTC, handling different DateTimeKind values
    /// </summary>
    public static DateTime ToUtc(DateTime dateTime)
    {
        return dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
        };
    }

    /// <summary>
    /// Converts a nullable DateTime to UTC
    /// </summary>
    public static DateTime? ToUtc(DateTime? dateTime)
    {
        return dateTime.HasValue ? ToUtc(dateTime.Value) : null;
    }

    /// <summary>
    /// Creates a UTC DateTime from date components
    /// </summary>
    public static DateTime CreateUtc(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
    {
        return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
    }

    /// <summary>
    /// Creates a UTC DateTime from a date string
    /// </summary>
    public static DateTime ParseUtc(string dateString)
    {
        var parsed = DateTime.Parse(dateString);
        return ToUtc(parsed);
    }

    /// <summary>
    /// Tries to parse a date string to UTC DateTime
    /// </summary>
    public static bool TryParseUtc(string dateString, out DateTime result)
    {
        if (DateTime.TryParse(dateString, out var parsed))
        {
            result = ToUtc(parsed);
            return true;
        }
        result = default;
        return false;
    }

    /// <summary>
    /// Gets the start of day in UTC
    /// </summary>
    public static DateTime StartOfDayUtc(DateTime date)
    {
        var utcDate = ToUtc(date);
        return new DateTime(utcDate.Year, utcDate.Month, utcDate.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>
    /// Gets the end of day in UTC
    /// </summary>
    public static DateTime EndOfDayUtc(DateTime date)
    {
        var utcDate = ToUtc(date);
        return new DateTime(utcDate.Year, utcDate.Month, utcDate.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1).AddTicks(-1);
    }

    /// <summary>
    /// Adds days to a DateTime and ensures result is UTC
    /// </summary>
    public static DateTime AddDaysUtc(DateTime date, double days)
    {
        return ToUtc(date).AddDays(days);
    }

    /// <summary>
    /// Adds months to a DateTime and ensures result is UTC
    /// </summary>
    public static DateTime AddMonthsUtc(DateTime date, int months)
    {
        return ToUtc(date).AddMonths(months);
    }

    /// <summary>
    /// Ensures a DateTime range is properly set to UTC
    /// </summary>
    public static (DateTime start, DateTime end) ToUtcRange(DateTime start, DateTime end)
    {
        return (ToUtc(start), ToUtc(end));
    }

    /// <summary>
    /// Compares two dates ignoring time component
    /// </summary>
    public static bool AreSameDate(DateTime date1, DateTime date2)
    {
        var utc1 = ToUtc(date1);
        var utc2 = ToUtc(date2);
        return utc1.Date == utc2.Date;
    }
}