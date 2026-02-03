namespace MyMascada.Domain.Enums;

/// <summary>
/// Represents the different fields that can be used in rule conditions
/// </summary>
public enum RuleConditionField
{
    /// <summary>
    /// Original transaction description from bank/import
    /// </summary>
    Description = 1,

    /// <summary>
    /// User-modified description
    /// </summary>
    UserDescription = 2,

    /// <summary>
    /// Transaction amount (absolute value)
    /// </summary>
    Amount = 3,

    /// <summary>
    /// Type of account (Checking, Savings, Credit Card, etc.)
    /// </summary>
    AccountType = 4,

    /// <summary>
    /// Name of the account
    /// </summary>
    AccountName = 5,

    /// <summary>
    /// Transaction type (Income, Expense, TransferComponent)
    /// </summary>
    TransactionType = 6,

    /// <summary>
    /// Reference number or check number
    /// </summary>
    ReferenceNumber = 7,

    /// <summary>
    /// Transaction notes
    /// </summary>
    Notes = 8
}