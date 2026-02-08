namespace MyMascada.Domain.Enums;

/// <summary>
/// Tracks the lifecycle of an account sharing invitation.
/// </summary>
public enum AccountShareStatus
{
    /// <summary>
    /// Invitation sent but not yet accepted or declined
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Recipient accepted the invitation and has access
    /// </summary>
    Accepted = 2,

    /// <summary>
    /// Recipient declined the invitation
    /// </summary>
    Declined = 3,

    /// <summary>
    /// Owner revoked the share (immediate access removal)
    /// </summary>
    Revoked = 4
}
