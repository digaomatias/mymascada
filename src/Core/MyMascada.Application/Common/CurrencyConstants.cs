using System.Text.RegularExpressions;

namespace MyMascada.Application.Common;

/// <summary>
/// Currency validation — accepts any syntactically valid 3-letter ISO 4217 code.
/// </summary>
public static class CurrencyConstants
{
    private static readonly Regex Iso4217Pattern = new(@"^[A-Z]{3}$", RegexOptions.Compiled);

    /// <summary>
    /// Returns true when the value is exactly three uppercase ASCII letters.
    /// </summary>
    public static bool IsValid(string? code) =>
        code is not null && Iso4217Pattern.IsMatch(code);
}
