namespace MyMascada.Domain.Enums;

/// <summary>
/// Represents the type of bank synchronization operation
/// </summary>
public enum BankSyncType
{
    /// <summary>
    /// User-initiated manual sync
    /// </summary>
    Manual = 1,

    /// <summary>
    /// Automatically scheduled periodic sync
    /// </summary>
    Scheduled = 2,

    /// <summary>
    /// Triggered by a webhook notification from the provider
    /// </summary>
    Webhook = 3,

    /// <summary>
    /// Initial sync when connection is first established
    /// </summary>
    Initial = 4
}

/// <summary>
/// Represents the status of a bank synchronization operation
/// </summary>
public enum BankSyncStatus
{
    /// <summary>
    /// Sync is currently in progress
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Sync completed successfully with all transactions processed
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Sync failed with errors
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Sync completed but some transactions could not be processed
    /// </summary>
    PartialSuccess = 4
}
