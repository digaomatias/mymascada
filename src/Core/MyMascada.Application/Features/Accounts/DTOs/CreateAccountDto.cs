using System.ComponentModel.DataAnnotations;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Accounts.DTOs;

/// <summary>
/// DTO for creating new accounts
/// </summary>
public class CreateAccountDto
{
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
    
    [Required]
    [Range(-1000000, 1000000, ErrorMessage = "Initial balance must be between -1,000,000 and 1,000,000")]
    public decimal InitialBalance { get; set; } = 0;
    
    [Required]
    [StringLength(3, MinimumLength = 3)]
    [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "Currency must be a 3-letter code (e.g., USD, EUR)")]
    public string Currency { get; set; } = "USD";
    
    [StringLength(500)]
    public string? Notes { get; set; }
    
    public bool IsActive { get; set; } = true;
}