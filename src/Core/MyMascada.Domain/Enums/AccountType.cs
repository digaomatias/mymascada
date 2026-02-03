namespace MyMascada.Domain.Enums;

/// <summary>
/// Represents the different types of financial accounts
/// </summary>
public enum AccountType
{
    /// <summary>
    /// Standard checking account for daily transactions
    /// </summary>
    Checking = 1,

    /// <summary>
    /// Savings account for storing money
    /// </summary>
    Savings = 2,

    /// <summary>
    /// Credit card account (negative balance represents debt)
    /// </summary>
    CreditCard = 3,

    /// <summary>
    /// Investment account (stocks, bonds, etc.)
    /// </summary>
    Investment = 4,

    /// <summary>
    /// Loan account (mortgage, personal loan, etc.)
    /// </summary>
    Loan = 5,

    /// <summary>
    /// Cash account for tracking physical money
    /// </summary>
    Cash = 6,

    /// <summary>
    /// Other type of account not covered by standard types
    /// </summary>
    Other = 99
}