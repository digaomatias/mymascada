using System;
using System.ComponentModel.DataAnnotations;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Transactions.DTOs;

/// <summary>
/// Request DTO for creating new transactions
/// </summary>
public class CreateTransactionRequest
{
    [Required]
    [Range(-1000000, 1000000, ErrorMessage = "Amount must be between -1,000,000 and 1,000,000")]
    public decimal Amount { get; set; }
    
    [Required]
    public DateTime TransactionDate { get; set; }
    
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Description { get; set; } = string.Empty;
    
    [StringLength(200)]
    public string? UserDescription { get; set; }
    
    [Required]
    public TransactionStatus Status { get; set; } = TransactionStatus.Cleared;
    
    [StringLength(500)]
    public string? Notes { get; set; }
    
    [StringLength(100)]
    public string? Location { get; set; }
    
    [StringLength(200)]
    public string? Tags { get; set; }
    
    [Required]
    public int AccountId { get; set; }
    
    public int? CategoryId { get; set; }
}