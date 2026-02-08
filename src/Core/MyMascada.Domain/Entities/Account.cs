using MyMascada.Domain.Common;
using MyMascada.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Domain.Entities;

/// <summary>
/// Represents a financial account (bank account, credit card, etc.)
/// that belongs to a user and contains transactions.
/// </summary>
public class Account : BaseEntity
{
    /// <summary>
    /// Display name for the account (e.g., "Main Checking", "Visa Credit Card")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of account (Checking, Savings, Credit Card, etc.)
    /// </summary>
    public AccountType Type { get; set; }

    /// <summary>
    /// Financial institution name (e.g., "Chase Bank", "Wells Fargo")
    /// </summary>
    [MaxLength(100)]
    public string? Institution { get; set; }

    /// <summary>
    /// Last 4 digits of account number for identification
    /// </summary>
    [MaxLength(4)]
    public string? LastFourDigits { get; set; }

    /// <summary>
    /// Current balance of the account
    /// </summary>
    public decimal CurrentBalance { get; set; }

    /// <summary>
    /// Currency code (USD, EUR, etc.)
    /// </summary>
    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Whether this account is currently active and being tracked
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional notes about the account
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// Date of the last completed reconciliation for this account
    /// </summary>
    public DateTime? LastReconciledDate { get; set; }

    /// <summary>
    /// Statement ending balance from the last completed reconciliation
    /// </summary>
    public decimal? LastReconciledBalance { get; set; }

    /// <summary>
    /// User ID who owns this account
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    // Navigation properties
    /// <summary>
    /// Collection of transactions associated with this account
    /// </summary>
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    /// <summary>
    /// Bank connection for automatic transaction synchronization (if connected)
    /// </summary>
    public BankConnection? BankConnection { get; set; }

    /// <summary>
    /// Sharing relationships for this account
    /// </summary>
    public ICollection<AccountShare> Shares { get; set; } = new List<AccountShare>();

    /// <summary>
    /// Calculates the total balance including pending transactions
    /// </summary>
    public decimal GetTotalBalance()
    {
        return Transactions
            .Where(t => !t.IsDeleted)
            .Sum(t => t.Amount);
    }

    /// <summary>
    /// Gets transactions for a specific date range
    /// </summary>
    public IEnumerable<Transaction> GetTransactions(DateTime startDate, DateTime endDate)
    {
        return Transactions
            .Where(t => !t.IsDeleted && 
                       t.TransactionDate >= startDate && 
                       t.TransactionDate <= endDate)
            .OrderByDescending(t => t.TransactionDate);
    }
}