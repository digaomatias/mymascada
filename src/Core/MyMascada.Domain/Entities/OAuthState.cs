namespace MyMascada.Domain.Entities;

/// <summary>
/// A server-side OAuth state parameter, persisted so it survives process restarts.
///
/// Deliberately does NOT derive from BaseEntity: this is ephemeral, single-use data.
/// Soft-delete semantics would keep consumed states queryable and defeat the whole
/// point of consuming them, and audit columns are meaningless for a 10-minute token.
/// Rows are hard-deleted on consume and swept on expiry.
/// </summary>
public class OAuthState
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The user the state is bound to. One live state per user — storing a new one
    /// replaces any existing state for that user.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The opaque state value echoed back by the OAuth provider.
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// UTC instant after which this state is no longer valid.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
