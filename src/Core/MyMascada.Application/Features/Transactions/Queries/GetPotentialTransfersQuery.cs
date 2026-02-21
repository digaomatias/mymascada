using MediatR;
using MyMascada.Application.Features.Transactions.DTOs;

namespace MyMascada.Application.Features.Transactions.Queries;

/// <summary>
/// Query to find potential transfer transactions that are not properly linked
/// </summary>
public class GetPotentialTransfersQuery : IRequest<PotentialTransfersResponse>
{
    /// <summary>
    /// User ID to filter transactions
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Amount tolerance for matching transfers (default: 0.01)
    /// </summary>
    public decimal AmountTolerance { get; set; } = 0.01m;

    /// <summary>
    /// Date tolerance in days for matching transfers (default: 3 days)
    /// </summary>
    public int DateToleranceDays { get; set; } = 3;

    /// <summary>
    /// Include transactions that are already reviewed (default: true)
    /// </summary>
    public bool IncludeReviewed { get; set; } = true;

    /// <summary>
    /// Minimum confidence score for potential matches (default: 0.5)
    /// </summary>
    public decimal MinConfidence { get; set; } = 0.5m;

    /// <summary>
    /// Whether to include transactions that are already part of transfers
    /// </summary>
    public bool IncludeExistingTransfers { get; set; } = false;
}