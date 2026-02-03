using System.ComponentModel.DataAnnotations;

namespace MyMascada.Application.Features.OfxImport.DTOs;

public class OfxImportRequest
{
    [Required]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    public int? AccountId { get; set; }
    
    public bool CreateAccountIfNotExists { get; set; } = false;
    
    public string? AccountName { get; set; }
}

public class OfxImportResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ImportedTransactionsCount { get; set; }
    public int SkippedTransactionsCount { get; set; }
    public int DuplicateTransactionsCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public OfxAccountInfo? AccountInfo { get; set; }
}

public class OfxAccountInfo
{
    public string BankId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public decimal? Balance { get; set; }
    public DateTime? BalanceDate { get; set; }
    public string Currency { get; set; } = "USD";
}

public class OfxTransaction
{
    public string TransactionType { get; set; } = string.Empty; // DEBIT, CREDIT, etc.
    public DateTime PostedDate { get; set; }
    public decimal Amount { get; set; }
    public string TransactionId { get; set; } = string.Empty; // FITID
    public string Name { get; set; } = string.Empty;
    public string? Memo { get; set; }
    public string? CheckNumber { get; set; }
    public string? ReferenceNumber { get; set; }
}

public class OfxParseResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public OfxAccountInfo? AccountInfo { get; set; }
    public List<OfxTransaction> Transactions { get; set; } = new();
    public DateTime? StatementStartDate { get; set; }
    public DateTime? StatementEndDate { get; set; }
}