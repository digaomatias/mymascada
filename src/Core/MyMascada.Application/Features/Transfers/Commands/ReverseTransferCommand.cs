using MediatR;

namespace MyMascada.Application.Features.Transfers.Commands;

/// <summary>
/// Command to reverse the direction of a transfer (swap source and destination)
/// </summary>
public class ReverseTransferCommand : IRequest<ReverseTransferResponse>
{
    /// <summary>
    /// The ID of the transfer to reverse
    /// </summary>
    public Guid TransferId { get; set; }
    
    /// <summary>
    /// The user ID making the request
    /// </summary>
    public Guid UserId { get; set; }
}

/// <summary>
/// Response from reversing a transfer
/// </summary>
public class ReverseTransferResponse
{
    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Status message
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// The new source account name after reversal
    /// </summary>
    public string NewSourceAccount { get; set; } = string.Empty;
    
    /// <summary>
    /// The new destination account name after reversal
    /// </summary>
    public string NewDestinationAccount { get; set; } = string.Empty;
}