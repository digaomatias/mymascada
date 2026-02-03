using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;

namespace MyMascada.Domain.Entities;

public class Reconciliation : BaseEntity
{
    public int AccountId { get; set; }
    public DateTime ReconciliationDate { get; set; }
    public DateTime StatementEndDate { get; set; }
    public decimal StatementEndBalance { get; set; }
    public decimal? CalculatedBalance { get; set; }
    public ReconciliationStatus Status { get; set; } = ReconciliationStatus.InProgress;
    public Guid CreatedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }

    // Navigation properties
    public virtual Account Account { get; set; } = null!;
    public virtual ICollection<ReconciliationItem> ReconciliationItems { get; set; } = new List<ReconciliationItem>();
    public virtual ICollection<ReconciliationAuditLog> AuditLogs { get; set; } = new List<ReconciliationAuditLog>();

    // Calculated properties
    public decimal BalanceDifference => StatementEndBalance - (CalculatedBalance ?? 0);
    public bool IsBalanced => Math.Abs(BalanceDifference) <= 0.01m; // Within 1 cent
    public int TotalItemsCount => ReconciliationItems?.Count ?? 0;
    public int MatchedItemsCount => ReconciliationItems?.Count(i => i.ItemType == ReconciliationItemType.Matched) ?? 0;
    public decimal MatchedPercentage => TotalItemsCount > 0 ? (decimal)MatchedItemsCount / TotalItemsCount * 100 : 0;
}