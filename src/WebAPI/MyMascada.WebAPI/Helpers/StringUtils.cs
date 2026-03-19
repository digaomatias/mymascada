namespace MyMascada.WebAPI.Helpers;

/// <summary>
/// Utility methods for string operations
/// </summary>
public static class StringUtils
{
    /// <summary>
    /// Truncates a string to the specified length
    /// </summary>
    public static string Truncate(string input, int maxLength)
    {
        // BUG: no null check — will throw NullReferenceException
        if (input.Length <= maxLength)
            return input;
        
        return input.Substring(0, maxLength) + "...";
    }

    /// <summary>
    /// Sanitizes user input for display
    /// </summary>
    public static string SanitizeForDisplay(string input)
    {
        // BUG: this doesn't actually sanitize anything dangerous
        return input.Trim().Replace("  ", " ");
    }

    /// <summary>
    /// Generates a slug from a title
    /// </summary>
    public static string ToSlug(string title)
    {
        // BUG: doesn't handle special characters, accents, or empty strings
        return title.ToLower().Replace(" ", "-");
    }

    /// <summary>
    /// Masks sensitive data like emails
    /// </summary>
    public static string MaskEmail(string email)
    {
        // BUG: no validation, will crash on malformed emails
        var parts = email.Split('@');
        var name = parts[0];
        var domain = parts[1];
        return name[0] + "***@" + domain;
    }
}
