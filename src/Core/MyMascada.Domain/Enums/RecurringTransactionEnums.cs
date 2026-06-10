namespace MyMascada.Domain.Enums;

/// <summary>
/// Frequency at which a user-created recurring transaction repeats
/// </summary>
public enum RecurrenceFrequency
{
    /// <summary>
    /// Repeats every 7 days
    /// </summary>
    Weekly = 1,

    /// <summary>
    /// Repeats every 14 days
    /// </summary>
    Fortnightly = 2,

    /// <summary>
    /// Repeats every calendar month (with end-of-month clamping)
    /// </summary>
    Monthly = 3,

    /// <summary>
    /// Repeats every calendar year (with leap-day clamping)
    /// </summary>
    Yearly = 4,

    /// <summary>
    /// Repeats every CustomIntervalDays days
    /// </summary>
    Custom = 5
}

/// <summary>
/// Outcome of a single scheduled occurrence of a recurring transaction
/// </summary>
public enum RecurringTransactionOccurrenceStatus
{
    /// <summary>
    /// A reminder notification was sent to the user (remind-only mode)
    /// </summary>
    Notified = 1,

    /// <summary>
    /// A real transaction was automatically created (auto-create mode)
    /// </summary>
    Created = 2,

    /// <summary>
    /// The occurrence was skipped (e.g., schedule was inactive or past its end date)
    /// </summary>
    Skipped = 3
}
