using MyMascada.Domain.Enums;
using MyMascada.Application.Features.CsvImport.DTOs;

namespace MyMascada.Application.Features.ImportReview.DTOs;

public record AnalyzeImportRequest
{
    public string Source { get; init; } = string.Empty; // "csv" or "ofx"
    public int AccountId { get; init; }
    public Guid UserId { get; init; }
    public IEnumerable<ImportCandidateDto> Candidates { get; init; } = new List<ImportCandidateDto>();
    public CsvImportData? CsvData { get; init; }
    public OfxImportData? OfxData { get; init; }
    public ImportAnalysisOptions Options { get; init; } = new();
}

public record CsvImportData
{
    public string Content { get; init; } = string.Empty; // Base64 encoded CSV content
    public CsvMappings Mappings { get; init; } = new();
    public bool HasHeader { get; init; } = true;
}

public record CsvMappings
{
    public string? AmountColumn { get; init; }
    public string? DateColumn { get; init; }
    public string? DescriptionColumn { get; init; }
    public string? ReferenceColumn { get; init; }
    public string? TypeColumn { get; init; }
    public string DateFormat { get; init; } = "yyyy-MM-dd";
    public string AmountConvention { get; init; } = "standard";
    public TypeValueMappings? TypeValueMappings { get; init; }
}


public record OfxImportData
{
    public string Content { get; init; } = string.Empty; // Base64 encoded OFX content
    public bool CreateAccount { get; init; } = false;
    public string? AccountName { get; init; }
}

public record ImportAnalysisOptions
{
    public int DateToleranceDays { get; init; } = 3;
    public decimal AmountTolerance { get; init; } = 0.01m;
    public double DescriptionSimilarityThreshold { get; init; } = 0.8;
    public bool IncludeManualTransactions { get; init; } = true;
    public bool IncludeRecentImports { get; init; } = true;
}

public record ImportAnalysisResult
{
    public string AnalysisId { get; init; } = string.Empty;
    public int AccountId { get; init; }
    public IEnumerable<ImportReviewItemDto> ReviewItems { get; init; } = new List<ImportReviewItemDto>();
    public ImportAnalysisStatistics Summary { get; init; } = new();
    public IEnumerable<string> AnalysisNotes { get; init; } = new List<string>();
    public IEnumerable<string> Warnings { get; init; } = new List<string>();
    public IEnumerable<string> Errors { get; init; } = new List<string>();
    public DateTime AnalyzedAt { get; init; } = DateTime.UtcNow;
}

public record ImportAnalysisStatistics
{
    public int TotalCandidates { get; init; }
    public int CleanImports { get; init; }
    public int ExactDuplicates { get; init; }
    public int PotentialDuplicates { get; init; }
    public int TransferConflicts { get; init; }
    public int ManualConflicts { get; init; }
    public int RequiresReview { get; init; }
}

public record ImportReviewItemDto
{
    public string Id { get; init; } = string.Empty;
    public ImportCandidateDto ImportCandidate { get; init; } = new();
    public IEnumerable<ConflictInfoDto> Conflicts { get; init; } = new List<ConflictInfoDto>();
    public ConflictResolution ReviewDecision { get; init; } = ConflictResolution.Pending;
    public string? UserNotes { get; init; }
    public bool IsProcessed { get; init; }
}

public record ImportCandidateDto
{
    public string TempId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime Date { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? ReferenceId { get; init; }
    public string? ExternalReferenceId { get; init; }
    public string? Category { get; init; }
    public string? Notes { get; init; }
    public TransactionType Type { get; init; }
    public TransactionStatus Status { get; init; } = TransactionStatus.Cleared;
    public int SourceRowNumber { get; init; }
}

public record ConflictInfoDto
{
    public ConflictType Type { get; init; }
    public ConflictSeverity Severity { get; init; }
    public string Message { get; init; } = string.Empty;
    public ExistingTransactionDto? ConflictingTransaction { get; init; }
    public decimal ConfidenceScore { get; init; }
}

public record ExistingTransactionDto
{
    public int Id { get; init; }
    public decimal Amount { get; init; }
    public DateTime TransactionDate { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? ReferenceId { get; init; }
    public string? ExternalReferenceId { get; init; }
    public TransactionSource Source { get; init; }
    public TransactionStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record ImportExecutionRequest
{
    public string AnalysisId { get; init; } = string.Empty;
    public int AccountId { get; init; } = 0; // Added to avoid dependency on cached analysis
    public Guid UserId { get; init; }
    public IEnumerable<ImportDecisionDto> Decisions { get; init; } = new List<ImportDecisionDto>();
    public bool SkipValidation { get; init; } = false;
}

public record ImportDecisionDto
{
    public string ReviewItemId { get; init; } = string.Empty;
    public ConflictResolution Decision { get; init; }
    public string? UserNotes { get; init; }
    public ImportCandidateDto? Candidate { get; init; } // Include candidate data to avoid cache dependency
}

public record ImportExecutionResult
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public ImportExecutionStatistics Statistics { get; init; } = new();
    public IEnumerable<string> Errors { get; init; } = new List<string>();
    public IEnumerable<string> Warnings { get; init; } = new List<string>();
    public IEnumerable<ImportedTransactionDto> ImportedTransactions { get; init; } = new List<ImportedTransactionDto>();
}

public record ImportExecutionStatistics
{
    public int TotalDecisions { get; init; }
    public int ImportedCount { get; init; }
    public int SkippedCount { get; init; }
    public int MergedCount { get; init; }
    public int ErrorCount { get; init; }
    public DateTime ExecutedAt { get; init; } = DateTime.UtcNow;
}

public record BulkActionRequest
{
    public string AnalysisId { get; init; } = string.Empty;
    public BulkActionType ActionType { get; init; }
    public ConflictType? TargetConflictType { get; init; }
    public ConflictResolution Resolution { get; init; }
}

public record BulkActionResult
{
    public bool IsSuccess { get; init; }
    public int AffectedItemsCount { get; init; }
    public string Message { get; init; } = string.Empty;
}

public enum ConflictType
{
    None,
    ExactDuplicate,
    PotentialDuplicate,
    TransferConflict,
    ManualEntryConflict,
    AmountMismatch,
    DateMismatch,
    CategoryConflict
}

public enum ConflictSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum ConflictResolution
{
    Pending,
    Import,
    Skip,
    MergeWithExisting,
    ReplaceExisting
}

public enum BulkActionType
{
    SkipAllExactDuplicates,
    ImportAllNonConflicts,
    SkipAllByConflictType,
    ImportAllByConflictType
}