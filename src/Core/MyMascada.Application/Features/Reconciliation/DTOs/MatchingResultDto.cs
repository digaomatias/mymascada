using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Reconciliation.DTOs;

public record MatchingResultDto
{
    public int ReconciliationId { get; init; }
    public int TotalBankTransactions { get; init; }
    public int TotalAppTransactions { get; init; }
    public int ExactMatches { get; init; }
    public int FuzzyMatches { get; init; }
    public int UnmatchedBank { get; init; }
    public int UnmatchedApp { get; init; }
    public decimal OverallMatchPercentage { get; init; }
    public IEnumerable<MatchedPairDto> MatchedPairs { get; init; } = new List<MatchedPairDto>();
    public IEnumerable<BankTransactionDto> UnmatchedBankTransactions { get; init; } = new List<BankTransactionDto>();
    public IEnumerable<TransactionDetailsDto> UnmatchedAppTransactions { get; init; } = new List<TransactionDetailsDto>();
}

public record MatchedPairDto
{
    public BankTransactionDto BankTransaction { get; init; } = new();
    public TransactionDetailsDto AppTransaction { get; init; } = new();
    public decimal MatchConfidence { get; init; }
    public MatchMethod MatchMethod { get; init; }
    public string MatchReason { get; init; } = string.Empty;
}

public record TransactionMatchRequest
{
    public int ReconciliationId { get; init; }
    public IEnumerable<BankTransactionDto> BankTransactions { get; init; } = new List<BankTransactionDto>();
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public decimal? ToleranceAmount { get; init; } = 0.01m;
    public bool UseDescriptionMatching { get; init; } = true;
    public bool UseDateRangeMatching { get; init; } = true;
    public int DateRangeToleranceDays { get; init; } = 2;
}

public record ManualMatchRequest
{
    public int ReconciliationId { get; init; }
    public int AppTransactionId { get; init; }
    public string BankTransactionId { get; init; } = string.Empty;
    public BankTransactionDto BankTransaction { get; init; } = new();
}

public record ReconciliationStatisticsDto
{
    public int ReconciliationId { get; init; }
    public decimal StatementBalance { get; init; }
    public decimal CalculatedBalance { get; init; }
    public decimal BalanceDifference { get; init; }
    public bool IsBalanced { get; init; }
    public int TotalItems { get; init; }
    public int MatchedItems { get; init; }
    public int UnmatchedAppItems { get; init; }
    public int UnmatchedBankItems { get; init; }
    public int AdjustmentItems { get; init; }
    public decimal MatchedPercentage { get; init; }
    public DateTime LastUpdated { get; init; }
}