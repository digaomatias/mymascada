namespace MyMascada.Domain.Enums;

public enum ReconciliationAction
{
    ReconciliationStarted = 0,
    TransactionMatched = 1,
    TransactionUnmatched = 2,
    AdjustmentAdded = 3,
    BankStatementImported = 4,
    ReconciliationCompleted = 5,
    ReconciliationCancelled = 6,
    ManualTransactionAdded = 7,
    TransactionDeleted = 8
}