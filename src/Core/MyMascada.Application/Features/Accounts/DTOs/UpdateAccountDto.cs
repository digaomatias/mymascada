using System.ComponentModel.DataAnnotations;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Accounts.DTOs;

/// <summary>
/// DTO for updating existing accounts
/// </summary>
public class UpdateAccountDto
{
    [Required]
    public int Id { get; set; }
    
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public AccountType Type { get; set; }
    
    [StringLength(100)]
    public string? Institution { get; set; }
    
    [StringLength(4)]
    [RegularExpression(@"^\d{4}$", ErrorMessage = "Last four digits must be exactly 4 numbers")]
    public string? LastFourDigits { get; set; }
    
    [StringLength(500)]
    public string? Notes { get; set; }
    
    public bool IsActive { get; set; }
    
    [Required]
    [StringLength(3, MinimumLength = 3)]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Currency must be a 3-letter ISO code (e.g., USD, EUR)")]
    public string Currency { get; set; } = "USD";
    
    // Note: CurrentBalance is not updated through this DTO
    // Balance changes should go through transactions
}