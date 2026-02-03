using MyMascada.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents a user decision to exclude specific transactions from duplicate detection.
/// This prevents the same potential duplicates from appearing repeatedly in scans.
/// </summary>
public class DuplicateExclusion : BaseEntity
{
    /// <summary>
    /// User ID who made the exclusion decision
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Comma-separated list of transaction IDs that were considered potential duplicates
    /// but the user marked them as "Not Duplicate". Stored sorted for consistent lookup.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string TransactionIds { get; set; } = string.Empty;

    /// <summary>
    /// Optional reason or notes for why these transactions were excluded
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>
    /// When this exclusion was created
    /// </summary>
    public DateTime ExcludedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Confidence score of the original duplicate detection for reference
    /// </summary>
    public decimal OriginalConfidence { get; set; }

    /// <summary>
    /// Navigation property to the user who made this decision
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Gets the transaction IDs as a list of integers
    /// </summary>
    public List<int> GetTransactionIdsList()
    {
        if (string.IsNullOrWhiteSpace(TransactionIds))
            return new List<int>();

        return TransactionIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(id => int.Parse(id.Trim()))
            .ToList();
    }

    /// <summary>
    /// Sets the transaction IDs from a list of integers (sorted for consistency)
    /// </summary>
    public void SetTransactionIdsList(IEnumerable<int> transactionIds)
    {
        var sortedIds = transactionIds.OrderBy(id => id).ToList();
        TransactionIds = string.Join(",", sortedIds);
    }

    /// <summary>
    /// Checks if this exclusion applies to a given set of transaction IDs
    /// </summary>
    public bool AppliesToTransactions(IEnumerable<int> transactionIds)
    {
        var thisIds = GetTransactionIdsList().ToHashSet();
        var checkIds = transactionIds.ToHashSet();
        
        // The exclusion applies if there's any overlap in transaction IDs
        // This handles cases where duplicate groups might have evolved slightly
        return thisIds.Intersect(checkIds).Any();
    }
}