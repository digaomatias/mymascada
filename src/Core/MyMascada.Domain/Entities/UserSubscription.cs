using System.ComponentModel.DataAnnotations;
using MyMascada.Domain.Common;

namespace MyMascada.Domain.Entities;

public class UserSubscription : BaseEntity
{
    public Guid UserId { get; set; }

    public int PlanId { get; set; }

    [MaxLength(100)]
    public string? StripeCustomerId { get; set; }

    [MaxLength(100)]
    public string? StripeSubscriptionId { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "free";

    public DateTime? CurrentPeriodStart { get; set; }

    public DateTime? CurrentPeriodEnd { get; set; }

    public DateTime? CancelledAt { get; set; }

    public BillingPlan Plan { get; set; } = null!;
}
