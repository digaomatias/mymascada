using MediatR;
using MyMascada.Application.Features.Transfers.DTOs;
using System.ComponentModel.DataAnnotations;

namespace MyMascada.Application.Features.Transfers.Commands;

/// <summary>
/// Command to create a transfer between two accounts
/// </summary>
public class CreateTransferCommand : IRequest<TransferDto>
{
    /// <summary>
    /// Source account ID (money leaving)
    /// </summary>
    [Required]
    public int SourceAccountId { get; set; }

    /// <summary>
    /// Destination account ID (money arriving)
    /// </summary>
    [Required]
    public int DestinationAccountId { get; set; }

    /// <summary>
    /// Transfer amount (always positive)
    /// </summary>
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Currency code for the transfer
    /// </summary>
    [Required]
    [StringLength(3)]
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Exchange rate if transferring between different currencies
    /// </summary>
    public decimal? ExchangeRate { get; set; }

    /// <summary>
    /// Fee amount for the transfer
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? FeeAmount { get; set; }

    /// <summary>
    /// Description or reason for the transfer
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Additional notes about the transfer
    /// </summary>
    [StringLength(1000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Date when the transfer occurred
    /// </summary>
    public DateTime TransferDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID performing the transfer
    /// </summary>
    [Required]
    public Guid UserId { get; set; }
}