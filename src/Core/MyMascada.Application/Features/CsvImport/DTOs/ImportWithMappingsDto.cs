namespace MyMascada.Application.Features.CsvImport.DTOs;

/// <summary>
/// Request to import CSV with confirmed column mappings
/// </summary>
public class ImportWithMappingsDto
{
    /// <summary>
    /// Base64 encoded CSV content
    /// </summary>
    public string CsvContent { get; set; } = string.Empty;

    /// <summary>
    /// The confirmed column mappings
    /// </summary>
    public CsvColumnMappings Mappings { get; set; } = new();

    /// <summary>
    /// Account ID to import transactions into
    /// </summary>
    public int? AccountId { get; set; }

    /// <summary>
    /// Name for new account if creating one
    /// </summary>
    public string? AccountName { get; set; }

    /// <summary>
    /// Whether to skip duplicate transactions
    /// </summary>
    public bool SkipDuplicates { get; set; } = true;

    /// <summary>
    /// Whether to auto-categorize imported transactions
    /// </summary>
    public bool AutoCategorize { get; set; } = true;

    /// <summary>
    /// Maximum number of rows to import (0 = all)
    /// </summary>
    public int MaxRows { get; set; } = 0;
}

/// <summary>
/// Request to analyze CSV structure with AI
/// </summary>
public class AnalyzeCsvRequest
{
    /// <summary>
    /// Optional account type hint for better analysis
    /// </summary>
    public string? AccountType { get; set; }

    /// <summary>
    /// Optional currency hint
    /// </summary>
    public string? CurrencyHint { get; set; }

    /// <summary>
    /// Number of sample rows to analyze (default 10)
    /// </summary>
    public int SampleSize { get; set; } = 10;
}