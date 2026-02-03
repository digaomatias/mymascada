using MyMascada.Domain.Enums;
using MyMascada.Application.Common.Converters;
using System.Text.Json.Serialization;

namespace MyMascada.Application.Features.Reconciliation.DTOs;

public record ReconciliationItemDto
{
    public int Id { get; init; }
    public int ReconciliationId { get; init; }
    public int? TransactionId { get; init; }
    public ReconciliationItemType ItemType { get; init; }
    public decimal? MatchConfidence { get; init; }
    public MatchMethod? MatchMethod { get; init; }
    public string? BankReferenceData { get; init; }
    
    // Transaction details (if linked)
    public TransactionDetailsDto? Transaction { get; init; }
    
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record TransactionDetailsDto
{
    public int Id { get; init; }
    public decimal Amount { get; init; }
    public string Description { get; init; } = string.Empty;
    public DateTime TransactionDate { get; init; }
    public string? CategoryName { get; init; }
    public TransactionStatus Status { get; init; }
}

public record CreateReconciliationItemDto
{
    public int ReconciliationId { get; init; }
    public int? TransactionId { get; init; }
    public ReconciliationItemType ItemType { get; init; }
    public decimal? MatchConfidence { get; init; }
    public MatchMethod? MatchMethod { get; init; }
    public object? BankReferenceData { get; init; }
}

public record UpdateReconciliationItemDto
{
    public int Id { get; init; }
    public int? TransactionId { get; init; }
    public ReconciliationItemType? ItemType { get; init; }
    public decimal? MatchConfidence { get; init; }
    public MatchMethod? MatchMethod { get; init; }
}

public record BankTransactionDto
{
    public string BankTransactionId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Description { get; init; } = string.Empty;
    [JsonConverter(typeof(UtcDateTimeConverter))]
    public DateTime TransactionDate { get; init; }
    public string? BankCategory { get; init; }
    public string? Reference { get; init; }
}