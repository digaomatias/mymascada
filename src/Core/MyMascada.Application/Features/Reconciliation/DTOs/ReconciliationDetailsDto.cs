using MyMascada.Domain.Enums;
using MyMascada.Application.Features.Reconciliation.Services;

namespace MyMascada.Application.Features.Reconciliation.DTOs;

public record ReconciliationDetailsDto
{
    public int ReconciliationId { get; init; }
    public ReconciliationDetailsSummaryDto Summary { get; init; } = new();
    public IEnumerable<ReconciliationItemDetailDto> ExactMatches { get; init; } = new List<ReconciliationItemDetailDto>();
    public IEnumerable<ReconciliationItemDetailDto> FuzzyMatches { get; init; } = new List<ReconciliationItemDetailDto>();
    public IEnumerable<ReconciliationItemDetailDto> UnmatchedBankTransactions { get; init; } = new List<ReconciliationItemDetailDto>();
    public IEnumerable<ReconciliationItemDetailDto> UnmatchedSystemTransactions { get; init; } = new List<ReconciliationItemDetailDto>();
}

public record ReconciliationDetailsSummaryDto
{
    public int TotalItems { get; init; }
    public int ExactMatches { get; init; }
    public int FuzzyMatches { get; init; }
    public int UnmatchedBank { get; init; }
    public int UnmatchedSystem { get; init; }
    public decimal MatchPercentage { get; init; }
}

public record ReconciliationItemDetailDto
{
    public int Id { get; init; }
    public int ReconciliationId { get; init; }
    public int? TransactionId { get; init; }
    public ReconciliationItemType ItemType { get; init; }
    public decimal? MatchConfidence { get; init; }
    public MatchMethod? MatchMethod { get; init; }
    public BankTransactionDto? BankTransaction { get; init; }
    public TransactionDetailsDto? SystemTransaction { get; init; }
    public MatchAnalysisDto? MatchAnalysis { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    
    // Computed properties for easier frontend usage
    public string DisplayAmount => FormatAmount(GetAmount());
    public string DisplayDate => GetDate().ToString("yyyy-MM-dd");
    public string DisplayDescription => GetDescription();
    public string MatchTypeLabel => GetMatchTypeLabel();
    public string MatchConfidenceLabel => GetMatchConfidenceLabel();
    
    private decimal GetAmount()
    {
        return SystemTransaction?.Amount ?? BankTransaction?.Amount ?? 0;
    }
    
    private DateTime GetDate()
    {
        return SystemTransaction?.TransactionDate ?? BankTransaction?.TransactionDate ?? DateTime.MinValue;
    }
    
    private string GetDescription()
    {
        return SystemTransaction?.Description ?? BankTransaction?.Description ?? string.Empty;
    }
    
    private string FormatAmount(decimal amount)
    {
        return amount.ToString("C");
    }
    
    private string GetMatchTypeLabel()
    {
        return ItemType switch
        {
            ReconciliationItemType.Matched when MatchConfidence >= 0.95m => "Exact Match",
            ReconciliationItemType.Matched => "Fuzzy Match",
            ReconciliationItemType.UnmatchedBank => "Unmatched Bank",
            ReconciliationItemType.UnmatchedApp => "Unmatched System",
            _ => "Unknown"
        };
    }
    
    private string GetMatchConfidenceLabel()
    {
        if (!MatchConfidence.HasValue || ItemType != ReconciliationItemType.Matched)
            return string.Empty;
            
        return MatchConfidence.Value switch
        {
            >= 0.95m => "High Confidence",
            >= 0.80m => "Medium Confidence", 
            >= 0.60m => "Low Confidence",
            _ => "Very Low Confidence"
        };
    }
}