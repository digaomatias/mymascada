using System;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Application.Features.Transactions.DTOs;

/// <summary>
/// DTO for creating new transactions
/// </summary>
public class CreateTransactionDto
{
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
    
    [Required]
    public int AccountId { get; set; }
    
    public int? CategoryId { get; set; }
    
    [StringLength(1000)]
    public string? Notes { get; set; }
    
    [StringLength(200)]
    public string? Location { get; set; }
    
    /// <summary>
    /// Transaction type: "income" or "expense"
    /// </summary>
    [Required]
    [RegularExpression("^(income|expense)$", ErrorMessage = "Type must be 'income' or 'expense'")]
    public string Type { get; set; } = "expense";
    
    /// <summary>
    /// Status: "pending", "cleared", "reconciled"
    /// </summary>
    public string Status { get; set; } = "cleared";
    
    [StringLength(100)]
    public string? ExternalId { get; set; }
    
    [StringLength(50)]
    public string? ReferenceNumber { get; set; }
    
    /// <summary>
    /// Comma-separated tags
    /// </summary>
    [StringLength(500)]
    public string? Tags { get; set; }
    
    public bool IsExcluded { get; set; } = false;
}