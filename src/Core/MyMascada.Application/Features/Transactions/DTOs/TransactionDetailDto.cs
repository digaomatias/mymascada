using System;
using System.Collections.Generic;

namespace MyMascada.Application.Features.Transactions.DTOs;

/// <summary>
/// Detailed transaction information for single transaction views
/// </summary>
public class TransactionDetailDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? UserDescription { get; set; }
    public string? Notes { get; set; }
    public string? Location { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty; // "Manual", "CsvImport", etc.
    public bool IsReviewed { get; set; }
    public bool IsExcluded { get; set; }
    
    // Reference information
    public string? ExternalId { get; set; }
    public string? ReferenceNumber { get; set; }
    public List<string> Tags { get; set; } = new List<string>();
    
    // Account information
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    
    // Category information
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryColor { get; set; }
    public string? CategoryIcon { get; set; }
    
    // Transfer information
    public Guid? TransferId { get; set; }
    public bool IsTransferSource { get; set; }
    public int? RelatedTransactionId { get; set; }
    public string? RelatedAccountName { get; set; }
    
    // Splits information
    public List<TransactionSplitDto>? Splits { get; set; }
    
    // Audit information
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

public class TransactionSplitDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryColor { get; set; }
}