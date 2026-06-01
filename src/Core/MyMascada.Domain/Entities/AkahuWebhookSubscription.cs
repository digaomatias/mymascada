using System.ComponentModel.DataAnnotations;
using MyMascada.Domain.Common;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Persisted record of an Akahu webhook subscription tied to a user's <see cref="AkahuUserCredential"/>.
/// One row per (UserId, WebhookType) at most. Mirrors Akahu's authoritative state so MyMascada
/// can issue DELETE /webhooks/{id} on disconnect and reconcile drift on a schedule.
/// </summary>
public class AkahuWebhookSubscription : BaseEntity
{
    /// <summary>
    /// The MyMascada user that owns this subscription. Also acts as the <c>state</c> value Akahu
    /// echoes back on every webhook delivery (encoded as a Guid "N" format string).
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// FK to <see cref="AkahuUserCredential"/>. Lets a credential delete cascade clean up subscriptions.
    /// </summary>
    [Required]
    public int AkahuUserCredentialId { get; set; }

    /// <summary>
    /// The Akahu webhook subscription ID returned by POST /webhooks (e.g. <c>whk_xxx</c>).
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string WebhookId { get; set; } = string.Empty;

    /// <summary>
    /// Akahu webhook type the subscription covers (one of TOKEN, ACCOUNT, TRANSACTION).
    /// </summary>
    [Required]
    [MaxLength(40)]
    public string WebhookType { get; set; } = string.Empty;

    /// <summary>
    /// Echo of the <c>state</c> value registered at subscription time.
    /// </summary>
    [MaxLength(100)]
    public string? State { get; set; }

    /// <summary>
    /// UTC timestamp when the row (and the underlying Akahu subscription) was created.
    /// </summary>
    public DateTime SubscribedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the reconciliation job last confirmed the subscription is healthy at Akahu.
    /// </summary>
    public DateTime? LastReconciledAt { get; set; }

    /// <summary>
    /// Last reconciliation error message (truncated). Null when the subscription is healthy.
    /// </summary>
    [MaxLength(500)]
    public string? LastReconcileError { get; set; }
}
