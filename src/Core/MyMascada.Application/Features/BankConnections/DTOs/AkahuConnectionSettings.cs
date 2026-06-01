namespace MyMascada.Application.Features.BankConnections.DTOs;

/// <summary>
/// Settings stored encrypted for an Akahu bank connection.
/// These are per-connection sync state stored in BankConnection.EncryptedSettings.
/// NOTE: Access tokens are now stored per-user in AkahuUserCredential, not per-connection.
/// </summary>
public class AkahuConnectionSettings
{
    /// <summary>
    /// Akahu account ID (acc_xxx) that this connection is linked to.
    /// </summary>
    public string AkahuAccountId { get; set; } = string.Empty;

    /// <summary>
    /// Last transaction ID that was successfully synced (for incremental sync).
    /// </summary>
    public string? LastSyncedTransactionId { get; set; }

    /// <summary>
    /// Timestamp of the last successful sync.
    /// </summary>
    public DateTime? LastSyncTimestamp { get; set; }
}
