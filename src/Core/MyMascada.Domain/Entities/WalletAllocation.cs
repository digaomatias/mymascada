using MyMascada.Domain.Common;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

public class WalletAllocation : BaseEntity
{
    [Required]
    public int WalletId { get; set; }

    [Required]
    public int TransactionId { get; set; }

    public decimal Amount { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    // Navigation properties
    public Wallet Wallet { get; set; } = null!;

    public Transaction Transaction { get; set; } = null!;
}
