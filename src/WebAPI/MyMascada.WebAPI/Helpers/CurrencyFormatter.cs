using System.Globalization;

namespace MyMascada.WebAPI.Helpers;

/// <summary>
/// Provides static methods for formatting monetary amounts in various currencies.
/// Negative amounts are displayed in accounting style (parentheses).
/// Thread-safe: all static fields are read-only or lazily initialized with thread-safe defaults.
/// </summary>
public static class CurrencyFormatter
{
    private static readonly CultureInfo NzCulture = CultureInfo.CreateSpecificCulture("en-NZ");
    private static readonly CultureInfo BrCulture = CultureInfo.CreateSpecificCulture("pt-BR");

    /// <summary>
    /// Lazily-built lookup from ISO 4217 currency code to a matching <see cref="CultureInfo"/>.
    /// Initialized once on first access; thread-safe via <see cref="Lazy{T}"/>.
    /// </summary>
    private static readonly Lazy<Dictionary<string, CultureInfo>> CurrencyToCulture = new(() =>
    {
        var map = new Dictionary<string, CultureInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                map.TryAdd(region.ISOCurrencySymbol, culture);
            }
            catch (ArgumentException)
            {
                // Some cultures don't have a valid region; skip them.
            }
        }
        return map;
    });

    /// <summary>
    /// Formats a decimal amount as New Zealand Dollars (NZD).
    /// Uses the "$" symbol with period as decimal separator.
    /// Negative amounts are shown in accounting style: ($1,234.56).
    /// </summary>
    /// <param name="amount">The monetary amount to format.</param>
    /// <returns>A formatted NZD currency string.</returns>
    public static string FormatNzd(decimal amount)
    {
        return FormatWithAccountingStyle(amount, NzCulture);
    }

    /// <summary>
    /// Formats a decimal amount as Brazilian Reais (BRL).
    /// Uses the "R$" symbol with comma as decimal separator and period as group separator.
    /// Negative amounts are shown in accounting style: (R$ 1.234,56).
    /// </summary>
    /// <param name="amount">The monetary amount to format.</param>
    /// <returns>A formatted BRL currency string.</returns>
    public static string FormatBrl(decimal amount)
    {
        return FormatWithAccountingStyle(amount, BrCulture);
    }

    /// <summary>
    /// Formats a decimal amount using the culture associated with the given ISO 4217 currency code.
    /// Falls back to invariant culture with the currency code as a prefix if no matching culture is found.
    /// Negative amounts are shown in accounting style (parentheses).
    /// </summary>
    /// <param name="amount">The monetary amount to format.</param>
    /// <param name="currencyCode">An ISO 4217 currency code (e.g. "USD", "EUR", "NZD", "BRL").</param>
    /// <returns>A formatted currency string.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="currencyCode"/> is null or whitespace.</exception>
    public static string FormatCurrency(decimal amount, string currencyCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);

        var code = currencyCode.Trim().ToUpperInvariant();

        var culture = FindCultureForCurrency(code);
        if (culture is not null)
        {
            return FormatWithAccountingStyle(amount, culture);
        }

        // Fallback: use invariant culture formatting with the currency code as prefix
        var formatted = Math.Abs(amount).ToString("N2", CultureInfo.InvariantCulture);
        return amount < 0
            ? $"({code} {formatted})"
            : $"{code} {formatted}";
    }

    /// <summary>
    /// Formats the amount using the given culture's currency format,
    /// wrapping negative values in parentheses (accounting style).
    /// </summary>
    private static string FormatWithAccountingStyle(decimal amount, CultureInfo culture)
    {
        if (amount < 0)
        {
            var positive = Math.Abs(amount).ToString("C2", culture);
            return $"({positive})";
        }

        return amount.ToString("C2", culture);
    }

    /// <summary>
    /// Attempts to find a <see cref="CultureInfo"/> whose region uses the specified ISO 4217 currency code.
    /// Uses a cached lookup for O(1) access after first initialization.
    /// </summary>
    private static CultureInfo? FindCultureForCurrency(string currencyCode)
    {
        // Fast path for known currencies
        return currencyCode switch
        {
            "NZD" => NzCulture,
            "BRL" => BrCulture,
            _ => CurrencyToCulture.Value.TryGetValue(currencyCode, out var culture) ? culture : null
        };
    }
}
