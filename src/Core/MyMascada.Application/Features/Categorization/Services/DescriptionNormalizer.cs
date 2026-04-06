using System.Text.RegularExpressions;

namespace MyMascada.Application.Features.Categorization.Services;

/// <summary>
/// Normalizes transaction descriptions for similarity matching.
/// Strips noise (dates, reference numbers, trailing numerics, special characters)
/// so that equivalent transactions group together regardless of variable parts.
///
/// Example:
///   "PAK N SAVE PETONE NZ 15/03/2026 #REF-12345" → "pak n save petone nz"
/// </summary>
public static partial class DescriptionNormalizer
{
    // Date patterns: dd/mm/yyyy, yyyy-mm-dd, dd-mm-yyyy, mm/dd/yyyy, dd.mm.yyyy
    [GeneratedRegex(@"\b\d{1,4}[/\-\.]\d{1,2}[/\-\.]\d{2,4}\b")]
    private static partial Regex DatePattern();

    // Reference numbers: #12345 or REF-12345 / REF:12345 (only when REF is a standalone prefix)
    [GeneratedRegex(@"#\d+|\bREF[:\-]?\d+\b", RegexOptions.IgnoreCase)]
    private static partial Regex RefPattern();

    // Trailing numeric sequences (card numbers, amounts, IDs at end of string)
    [GeneratedRegex(@"\s+\d{3,}$")]
    private static partial Regex TrailingNumbersPattern();

    // Special characters — keep Unicode letters, digits, spaces, and hyphens
    [GeneratedRegex(@"[^\p{L}\p{N}\s\-]")]
    private static partial Regex SpecialCharsPattern();

    // Collapse multiple spaces/hyphens to single space
    [GeneratedRegex(@"[\s\-]+")]
    private static partial Regex WhitespacePattern();

    /// <summary>
    /// Normalizes a transaction description for matching purposes.
    /// </summary>
    public static string Normalize(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return string.Empty;

        var result = description.ToLowerInvariant();

        // 1. Remove date patterns
        result = DatePattern().Replace(result, " ");

        // 2. Remove reference number patterns
        result = RefPattern().Replace(result, " ");

        // 3. Remove trailing numeric sequences
        result = TrailingNumbersPattern().Replace(result, "");

        // 4. Remove special characters (keep letters, digits, spaces, hyphens)
        result = SpecialCharsPattern().Replace(result, " ");

        // 5. Collapse whitespace and hyphens to single space
        result = WhitespacePattern().Replace(result, " ");

        // 6. Trim
        return result.Trim();
    }

    // Common stop words to exclude from token matching
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "from", "with", "via", "nzd", "aud", "usd", "eur", "gbp"
    };

    /// <summary>
    /// Extracts significant tokens from a normalized description for fuzzy matching.
    /// Returns words longer than 3 chars that are not stop words.
    /// </summary>
    public static IReadOnlyList<string> ExtractTokens(string normalizedDescription)
    {
        if (string.IsNullOrWhiteSpace(normalizedDescription))
            return [];

        return normalizedDescription
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 3 && !StopWords.Contains(t))
            .Distinct()
            .ToList();
    }
}
