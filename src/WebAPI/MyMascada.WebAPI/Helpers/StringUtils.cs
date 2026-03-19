using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace MyMascada.WebAPI.Helpers;

/// <summary>
/// Utility methods for string operations
/// </summary>
public static partial class StringUtils
{
    /// <summary>
    /// Truncates a string to the specified length (including ellipsis)
    /// </summary>
    public static string Truncate(string input, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);

        if (input.Length <= maxLength)
            return input;

        if (maxLength <= 3)
            return "..."[..maxLength];

        return input[..Math.Max(0, maxLength - 3)] + "...";
    }

    /// <summary>
    /// Sanitizes user input for safe HTML display by encoding dangerous characters
    /// and normalizing whitespace
    /// </summary>
    public static string SanitizeForDisplay(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var encoded = HttpUtility.HtmlEncode(input);
        return NormalizeWhitespaceRegex().Replace(encoded.Trim(), " ");
    }

    /// <summary>
    /// Generates a URL-safe slug from a title, stripping accents and special characters
    /// </summary>
    public static string ToSlug(string title)
    {
        ArgumentNullException.ThrowIfNull(title);

        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        // Normalize and strip accents
        var normalized = title.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var slug = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();

        // Replace non-alphanumeric with hyphens
        slug = NonAlphanumericRegex().Replace(slug, "-");

        // Collapse consecutive hyphens and trim
        slug = ConsecutiveHyphensRegex().Replace(slug, "-").Trim('-');

        return slug;
    }

    /// <summary>
    /// Masks sensitive data like emails (e.g. "user@example.com" → "u***@example.com")
    /// </summary>
    public static string MaskEmail(string email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            return "***";

        var parts = email.Split('@', 2);
        var name = parts[0];

        if (name.Length <= 1)
            return "***@" + parts[1];

        return name[0] + "***@" + parts[1];
    }

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex NormalizeWhitespaceRegex();

    [GeneratedRegex(@"[^a-z0-9\s-]")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex ConsecutiveHyphensRegex();
}
