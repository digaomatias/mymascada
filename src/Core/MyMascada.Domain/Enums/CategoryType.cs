namespace MyMascada.Domain.Enums;

/// <summary>
/// Represents the different types of transaction categories
/// </summary>
public enum CategoryType
{
    /// <summary>
    /// Money coming into accounts (salary, dividends, etc.)
    /// </summary>
    Income = 1,

    /// <summary>
    /// Money going out of accounts (purchases, bills, etc.)
    /// </summary>
    Expense = 2,

    /// <summary>
    /// Money moving between accounts (no net gain/loss)
    /// </summary>
    Transfer = 3
}