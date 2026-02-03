using MediatR;
using MyMascada.Application.Features.Transactions.DTOs;

namespace MyMascada.Application.Features.Transactions.Commands;

/// <summary>
/// Command to link two existing transactions as a transfer
/// </summary>
public class LinkTransactionsAsTransferCommand : IRequest<ConfirmTransfersResponse>
{
    /// <summary>
    /// User ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Source transaction ID (outgoing)
    /// </summary>
    public int SourceTransactionId { get; set; }

    /// <summary>
    /// Destination transaction ID (incoming)
    /// </summary>
    public int DestinationTransactionId { get; set; }

    /// <summary>
    /// Optional description for the transfer
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional notes for the transfer
    /// </summary>
    public string? Notes { get; set; }
}