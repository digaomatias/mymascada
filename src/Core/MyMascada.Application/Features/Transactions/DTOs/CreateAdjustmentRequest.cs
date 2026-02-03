using System.ComponentModel.DataAnnotations;

namespace MyMascada.Application.Features.Transactions.DTOs;

/// <summary>
/// Request DTO for creating balance adjustment transactions
/// </summary>
public class CreateAdjustmentRequest
{
    [Required]
    [Range(-1000000, 1000000, ErrorMessage = "Amount must be between -1,000,000 and 1,000,000")]
    public decimal Amount { get; set; }
    
    [StringLength(200)]
    public string? Description { get; set; }
    
    [StringLength(500)]
    public string? Notes { get; set; }
    
    [Required]
    public int AccountId { get; set; }
}