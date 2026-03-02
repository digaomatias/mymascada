using System.ComponentModel.DataAnnotations;
using MyMascada.Domain.Common;

namespace MyMascada.Domain.Entities;

public class BillingPlan : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(100)]
    public string StripePriceId { get; set; } = string.Empty;

    public int MaxAccounts { get; set; }

    public int MaxTransactionsPerMonth { get; set; }

    public int MaxAiCallsPerMonth { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public ICollection<UserSubscription> Subscriptions { get; set; } = new List<UserSubscription>();
}
