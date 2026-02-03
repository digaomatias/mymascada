using System;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Transactions.DTOs;

/// <summary>
/// Basic transaction information for list views
/// </summary>
public class TransactionDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? UserDescription { get; set; }
    public TransactionStatus Status { get; set; }
    public TransactionSource Source { get; set; }
    public string? ExternalId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public string? Location { get; set; }
    public bool IsReviewed { get; set; }
    public bool IsExcluded { get; set; }
    public string? Tags { get; set; }
    public TransactionType Type { get; set; }
    
    // Account information
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    
    // Category information
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryColor { get; set; }
    
    // Transfer information
    public Guid? TransferId { get; set; }
    public bool IsTransferSource { get; set; }
    public int? RelatedTransactionId { get; set; }
    
    // Audit information
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}