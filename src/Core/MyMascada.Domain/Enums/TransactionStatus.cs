namespace MyMascada.Domain.Enums;

/// <summary>
/// Represents the status of a transaction in the account
/// </summary>
public enum TransactionStatus
{
    /// <summary>
    /// Transaction is pending and may not be final
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Transaction has been processed and cleared by the bank
    /// </summary>
    Cleared = 2,

    /// <summary>
    /// Transaction has been reconciled with bank statements
    /// </summary>
    Reconciled = 3,

    /// <summary>
    /// Transaction was cancelled or voided
    /// </summary>
    Cancelled = 4
}

/// <summary>
/// Represents the source of transaction data
/// </summary>
public enum TransactionSource
{
    /// <summary>
    /// Manually entered by user
    /// </summary>
    Manual = 1,

    /// <summary>
    /// Imported from CSV file
    /// </summary>
    CsvImport = 2,

    /// <summary>
    /// Retrieved from bank API
    /// </summary>
    BankApi = 3,

    /// <summary>
    /// Imported from OFX/QFX file
    /// </summary>
    OfxImport = 4,

    /// <summary>
    /// Imported from other financial software
    /// </summary>
    Import = 5
}

/// <summary>
/// Represents the type of transaction for income/expense classification
/// </summary>
public enum TransactionType
{
    /// <summary>
    /// Money coming into the account (positive amount)
    /// </summary>
    Income = 1,

    /// <summary>
    /// Money going out of the account (negative amount)
    /// </summary>
    Expense = 2,

    /// <summary>
    /// Part of a transfer between accounts
    /// </summary>
    TransferComponent = 3
}

/// <summary>
/// Represents the status of a transfer between accounts
/// </summary>
public enum TransferStatus
{
    /// <summary>
    /// Transfer is being processed
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Transfer completed successfully
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Transfer failed and was rolled back
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Transfer was cancelled before completion
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// Transfer was reversed after completion
    /// </summary>
    Reversed = 5
}