using System;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Application.Features.Transactions.DTOs;

/// <summary>
/// DTO for updating existing transactions
/// </summary>
public class UpdateTransactionDto
{
    [Required]
    public int Id { get; set; }
    
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }
    
    [Required]
    public DateTime TransactionDate { get; set; }
    
    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? UserDescription { get; set; }
    
    public int? CategoryId { get; set; }
    
    [StringLength(1000)]
    public string? Notes { get; set; }
    
    [StringLength(200)]
    public string? Location { get; set; }
    
    /// <summary>
    /// Status: "pending", "cleared", "reconciled", "cancelled"
    /// </summary>
    [Required]
    [RegularExpression("^(pending|cleared|reconciled|cancelled)$", ErrorMessage = "Invalid status")]
    public string Status { get; set; } = "cleared";
    
    [StringLength(50)]
    public string? ReferenceNumber { get; set; }
    
    /// <summary>
    /// Comma-separated tags
    /// </summary>
    [StringLength(500)]
    public string? Tags { get; set; }
    
    public bool IsReviewed { get; set; }
    public bool IsExcluded { get; set; }
}