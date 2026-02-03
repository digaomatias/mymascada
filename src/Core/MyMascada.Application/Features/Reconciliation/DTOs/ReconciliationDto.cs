using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Reconciliation.DTOs;

public record ReconciliationDto
{
    public int Id { get; init; }
    public int AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public DateTime ReconciliationDate { get; init; }
    public DateTime StatementEndDate { get; init; }
    public decimal StatementEndBalance { get; init; }
    public decimal? CalculatedBalance { get; init; }
    public ReconciliationStatus Status { get; init; }
    public Guid CreatedByUserId { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? Notes { get; init; }
    
    // Calculated properties
    public decimal BalanceDifference { get; init; }
    public bool IsBalanced { get; init; }
    public int TotalItemsCount { get; init; }
    public int MatchedItemsCount { get; init; }
    public decimal MatchedPercentage { get; init; }
    
    // Audit info
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record ReconciliationSummaryDto
{
    public int Id { get; init; }
    public int AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public DateTime ReconciliationDate { get; init; }
    public DateTime StatementEndDate { get; init; }
    public decimal StatementEndBalance { get; init; }
    public ReconciliationStatus Status { get; init; }
    public decimal BalanceDifference { get; init; }
    public bool IsBalanced { get; init; }
    public decimal MatchedPercentage { get; init; }
}

public record CreateReconciliationDto
{
    public int AccountId { get; init; }
    public DateTime StatementEndDate { get; init; }
    public decimal StatementEndBalance { get; init; }
    public string? Notes { get; init; }
}

public record UpdateReconciliationDto
{
    public int Id { get; init; }
    public DateTime? StatementEndDate { get; init; }
    public decimal? StatementEndBalance { get; init; }
    public ReconciliationStatus? Status { get; init; }
    public string? Notes { get; init; }
}

public record ReconciliationListResponse
{
    public IEnumerable<ReconciliationSummaryDto> Reconciliations { get; init; } = new List<ReconciliationSummaryDto>();
    public int TotalCount { get; init; }
    public int PageSize { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public bool HasNextPage { get; init; }
    public bool HasPreviousPage { get; init; }
}