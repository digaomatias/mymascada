using MediatR;
using MyMascada.Application.Features.Transactions.DTOs;

namespace MyMascada.Application.Features.Transactions.Commands;

/// <summary>
/// Command to create a missing transfer transaction for an unmatched transfer
/// </summary>
public class CreateMissingTransferCommand : IRequest<ConfirmTransfersResponse>
{
    /// <summary>
    /// User ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The existing transaction ID
    /// </summary>
    public int ExistingTransactionId { get; set; }

    /// <summary>
    /// The account ID for the missing transaction
    /// </summary>
    public int MissingAccountId { get; set; }

    /// <summary>
    /// Optional description for the transfer
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional notes for the transfer
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Optional different date for the missing transaction
    /// </summary>
    public DateTime? TransactionDate { get; set; }
}