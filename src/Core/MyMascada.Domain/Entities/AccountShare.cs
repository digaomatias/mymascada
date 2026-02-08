using System.ComponentModel.DataAnnotations;
using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents a sharing relationship between an account owner and another user.
/// Tracks invitation lifecycle and access role.
/// </summary>
public class AccountShare : BaseEntity
{
    /// <summary>
    /// The account being shared
    /// </summary>
    [Required]
    public int AccountId { get; set; }

    /// <summary>
    /// The user who receives access to the account
    /// </summary>
    [Required]
    public Guid SharedWithUserId { get; set; }

    /// <summary>
    /// The user who owns the account and initiated the share
    /// </summary>
    [Required]
    public Guid SharedByUserId { get; set; }

    /// <summary>
    /// Level of access granted (Viewer or Manager)
    /// </summary>
    public AccountShareRole Role { get; set; }

    /// <summary>
    /// Current status of the sharing invitation
    /// </summary>
    public AccountShareStatus Status { get; set; }

    /// <summary>
    /// SHA-256 hash of the invitation token (null after acceptance/decline)
    /// </summary>
    [MaxLength(64)]
    public string? InvitationToken { get; set; }

    /// <summary>
    /// When the invitation token expires (7 days from creation)
    /// </summary>
    public DateTime? InvitationExpiresAt { get; set; }

    // Navigation properties

    /// <summary>
    /// The account being shared
    /// </summary>
    public Account Account { get; set; } = null!;

    /// <summary>
    /// The user who receives access
    /// </summary>
    public User SharedWithUser { get; set; } = null!;

    /// <summary>
    /// The user who owns the account and initiated the share
    /// </summary>
    public User SharedByUser { get; set; } = null!;
}
