namespace MyMascada.Domain.Common;

/// <summary>
/// Extension methods for DateTime to ensure UTC compatibility
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Converts any DateTime to UTC
    /// </summary>
    public static DateTime ToUtc(this DateTime dateTime)
    {
        return DateTimeProvider.ToUtc(dateTime);
    }

    /// <summary>
    /// Converts a nullable DateTime to UTC
    /// </summary>
    public static DateTime? ToUtc(this DateTime? dateTime)
    {
        return DateTimeProvider.ToUtc(dateTime);
    }

    /// <summary>
    /// Gets the start of day in UTC
    /// </summary>
    public static DateTime StartOfDayUtc(this DateTime date)
    {
        return DateTimeProvider.StartOfDayUtc(date);
    }

    /// <summary>
    /// Gets the end of day in UTC
    /// </summary>
    public static DateTime EndOfDayUtc(this DateTime date)
    {
        return DateTimeProvider.EndOfDayUtc(date);
    }

    /// <summary>
    /// Adds days and ensures result is UTC
    /// </summary>
    public static DateTime AddDaysUtc(this DateTime date, double days)
    {
        return DateTimeProvider.AddDaysUtc(date, days);
    }

    /// <summary>
    /// Adds months and ensures result is UTC
    /// </summary>
    public static DateTime AddMonthsUtc(this DateTime date, int months)
    {
        return DateTimeProvider.AddMonthsUtc(date, months);
    }

    /// <summary>
    /// Checks if two dates are the same day (ignoring time)
    /// </summary>
    public static bool IsSameDate(this DateTime date1, DateTime date2)
    {
        return DateTimeProvider.AreSameDate(date1, date2);
    }
}