namespace MyMascada.Domain.Enums;

/// <summary>
/// Represents the different time periods for budget tracking
/// </summary>
public enum BudgetPeriodType
{
    /// <summary>
    /// Monthly budget period (default, most common)
    /// </summary>
    Monthly = 1,

    /// <summary>
    /// Weekly budget period
    /// </summary>
    Weekly = 2,

    /// <summary>
    /// Biweekly (every two weeks) budget period
    /// </summary>
    Biweekly = 3,

    /// <summary>
    /// Custom date range budget period
    /// </summary>
    Custom = 4
}
