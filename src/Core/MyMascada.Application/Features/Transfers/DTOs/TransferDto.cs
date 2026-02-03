using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Transfers.DTOs;

/// <summary>
/// Data transfer object for Transfer information
/// </summary>
public class TransferDto
{
    /// <summary>
    /// Transfer's database ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Unique transfer identifier
    /// </summary>
    public Guid TransferId { get; set; }

    /// <summary>
    /// Transfer amount (always positive)
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Currency code
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Exchange rate if multi-currency
    /// </summary>
    public decimal? ExchangeRate { get; set; }

    /// <summary>
    /// Fee amount for the transfer
    /// </summary>
    public decimal? FeeAmount { get; set; }

    /// <summary>
    /// Transfer description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Additional notes
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Current transfer status
    /// </summary>
    public TransferStatus Status { get; set; }

    /// <summary>
    /// Transfer initiation date
    /// </summary>
    public DateTime TransferDate { get; set; }

    /// <summary>
    /// Transfer completion date
    /// </summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>
    /// Source account information
    /// </summary>
    public TransferAccountDto SourceAccount { get; set; } = new();

    /// <summary>
    /// Destination account information
    /// </summary>
    public TransferAccountDto DestinationAccount { get; set; } = new();

    /// <summary>
    /// Related transactions (should be exactly 2)
    /// </summary>
    public List<TransferTransactionDto> Transactions { get; set; } = new();

    /// <summary>
    /// Whether this involves different currencies
    /// </summary>
    public bool IsMultiCurrency { get; set; }

    /// <summary>
    /// Effective destination amount considering exchange rate
    /// </summary>
    public decimal DestinationAmount { get; set; }

    /// <summary>
    /// Audit information
    /// </summary>
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Simplified account information for transfers
/// </summary>
public class TransferAccountDto
{
    /// <summary>
    /// Account ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Account name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Account currency
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Account type
    /// </summary>
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Transaction information for transfers
/// </summary>
public class TransferTransactionDto
{
    /// <summary>
    /// Transaction ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Transaction amount (negative for source, positive for destination)
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Transaction description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is the source side of the transfer
    /// </summary>
    public bool IsTransferSource { get; set; }

    /// <summary>
    /// Account ID this transaction belongs to
    /// </summary>
    public int AccountId { get; set; }

    /// <summary>
    /// Transaction type
    /// </summary>
    public TransactionType Type { get; set; }
}