using MyMascada.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

public class Wallet : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Icon { get; set; }

    [MaxLength(7)]
    public string? Color { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = string.Empty;

    public bool IsArchived { get; set; }

    public decimal? TargetAmount { get; set; }

    // Navigation properties
    public ICollection<WalletAllocation> Allocations { get; set; } = new List<WalletAllocation>();
}
