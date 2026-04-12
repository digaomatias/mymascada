namespace MyMascada.Application.Common;

/// <summary>
/// Currencies accepted across the application. Must stay in sync with frontend/src/lib/countries.ts.
/// </summary>
public static class CurrencyConstants
{
    public static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "EUR", "GBP", "BRL", "NZD", "AUD", "CAD", "JPY",
        "ARS", "CLP", "COP", "MXN"
    };
}
