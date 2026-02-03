using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.CsvImport.DTOs;

/// <summary>
/// Represents a potential transfer detected during CSV import
/// </summary>
public class TransferCandidate
{
    /// <summary>
    /// The debit transaction (money leaving source account)
    /// </summary>
    public TransactionDto DebitTransaction { get; set; } = null!;
    
    /// <summary>
    /// The credit transaction (money entering destination account)
    /// </summary>
    public TransactionDto CreditTransaction { get; set; } = null!;
    
    /// <summary>
    /// Confidence score from 0.0 to 1.0 indicating how likely this is a transfer
    /// </summary>
    public decimal ConfidenceScore { get; set; }
    
    /// <summary>
    /// Reasons why this was detected as a potential transfer
    /// </summary>
    public List<string> MatchingCriteria { get; set; } = new();
    
    /// <summary>
    /// Amount of the transfer (always positive)
    /// </summary>
    public decimal Amount { get; set; }
    
    /// <summary>
    /// Date of the transfer
    /// </summary>
    public DateTime TransferDate { get; set; }
}

/// <summary>
/// Configuration for transfer detection algorithm
/// </summary>
public class TransferDetectionConfig
{
    /// <summary>
    /// Maximum number of days difference between matching transactions
    /// </summary>
    public int MaxDaysDifference { get; set; } = 1;
    
    /// <summary>
    /// Maximum percentage difference in amounts to consider a match
    /// </summary>
    public decimal MaxAmountDifferencePercent { get; set; } = 0.05m; // 5%
    
    /// <summary>
    /// Minimum confidence score to suggest a transfer
    /// </summary>
    public decimal MinimumConfidenceScore { get; set; } = 0.7m;
    
    /// <summary>
    /// Keywords that increase confidence when found in descriptions
    /// </summary>
    public List<string> TransferKeywords { get; set; } = new()
    {
        "TRANSFER", "XFER", "ACH", "WIRE", "DEPOSIT", "WITHDRAWAL", 
        "FROM", "TO", "MOVE", "ONLINE TRANSFER", "BANK TRANSFER"
    };
    
    /// <summary>
    /// Whether to ignore case when matching keywords
    /// </summary>
    public bool IgnoreCase { get; set; } = true;
}

/// <summary>
/// Request to confirm or reject detected transfer candidates
/// </summary>
public class ConfirmTransferCandidateRequest
{
    /// <summary>
    /// IDs of the two transactions to link as a transfer
    /// </summary>
    public int DebitTransactionId { get; set; }
    public int CreditTransactionId { get; set; }
    
    /// <summary>
    /// Optional description for the transfer
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Whether to confirm (true) or reject (false) this candidate
    /// </summary>
    public bool IsConfirmed { get; set; }
}

/// <summary>
/// Response containing detected transfer candidates
/// </summary>
public class TransferDetectionResult
{
    /// <summary>
    /// List of potential transfers found
    /// </summary>
    public List<TransferCandidate> Candidates { get; set; } = new();
    
    /// <summary>
    /// Number of transactions that were analyzed
    /// </summary>
    public int TransactionsAnalyzed { get; set; }
    
    /// <summary>
    /// Number of potential transfers found
    /// </summary>
    public int CandidatesFound { get; set; }
    
    /// <summary>
    /// Processing time in milliseconds
    /// </summary>
    public long ProcessingTimeMs { get; set; }
}