using System.Collections.Generic;

namespace MyMascada.Application.Features.Transactions.DTOs;

/// <summary>
/// Request body for PUT /api/v1/transactions/{id}/splits.
/// Replaces the transaction's splits atomically; a null or empty list clears
/// all splits (un-splits the transaction).
/// </summary>
public class UpdateTransactionSplitsRequest
{
    public List<TransactionSplitInputDto>? Splits { get; set; }
}

/// <summary>
/// A single split item supplied by the client. Mirrors the writable subset of
/// <see cref="TransactionSplitDto"/> (category, amount, optional description).
/// </summary>
public class TransactionSplitInputDto
{
    public int CategoryId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}
