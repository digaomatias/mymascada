namespace MyMascada.Domain.Enums;

/// <summary>
/// Represents the status of a recurring pattern (subscription/bill)
/// </summary>
public enum RecurringPatternStatus
{
    /// <summary>
    /// Pattern is active and being tracked. Payments are expected at regular intervals.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Pattern has missed one expected payment and is at risk of being cancelled.
    /// Grace window has expired without a matching transaction.
    /// </summary>
    AtRisk = 2,

    /// <summary>
    /// Pattern has been temporarily paused by the user.
    /// No alerts will be generated and missed payments won't affect status.
    /// </summary>
    Paused = 3,

    /// <summary>
    /// Pattern has been cancelled (either by user or automatically after 2 consecutive misses).
    /// Can be reactivated if a matching transaction appears.
    /// </summary>
    Cancelled = 4
}

/// <summary>
/// Represents the outcome of an expected recurring occurrence
/// </summary>
public enum OccurrenceOutcome
{
    /// <summary>
    /// A matching transaction was posted before or within the grace window
    /// </summary>
    Posted = 1,

    /// <summary>
    /// No matching transaction was found within the grace window
    /// </summary>
    Missed = 2,

    /// <summary>
    /// A matching transaction was posted but after the expected date (within grace window)
    /// </summary>
    Late = 3
}
