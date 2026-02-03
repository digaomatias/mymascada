using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;
using System.Text.Json;

namespace MyMascada.Domain.Entities;

public class ReconciliationItem : BaseEntity
{
    public int ReconciliationId { get; set; }
    public int? TransactionId { get; set; }
    public ReconciliationItemType ItemType { get; set; }
    public decimal? MatchConfidence { get; set; }
    public MatchMethod? MatchMethod { get; set; }
    public string? BankReferenceData { get; set; } // JSON string for bank statement data

    /// <summary>
    /// Whether this matched item has been approved by the user.
    /// Approval enriches the system transaction with bank data and applies category mappings.
    /// </summary>
    public bool IsApproved { get; set; }

    /// <summary>
    /// When the match was approved by the user.
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    // Navigation properties
    public virtual Reconciliation Reconciliation { get; set; } = null!;
    public virtual Transaction? Transaction { get; set; }

    // Helper methods for bank reference data
    public void SetBankReferenceData<T>(T data) where T : class
    {
        BankReferenceData = data != null ? JsonSerializer.Serialize(data) : null;
    }

    public T? GetBankReferenceData<T>() where T : class
    {
        if (string.IsNullOrEmpty(BankReferenceData))
            return null;
        
        try
        {
            return JsonSerializer.Deserialize<T>(BankReferenceData);
        }
        catch
        {
            return null;
        }
    }
}

// Helper class for bank statement data
public class BankStatementItemData
{
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? TransactionType { get; set; }
}