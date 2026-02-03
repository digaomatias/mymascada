namespace MyMascada.Application.Features.CsvImport.DTOs;

/// <summary>
/// Result of AI-powered CSV structure analysis
/// </summary>
public class CsvAnalysisResultDto
{
    /// <summary>
    /// Suggested column mappings with confidence scores
    /// </summary>
    public Dictionary<string, ColumnMappingDto> SuggestedMappings { get; set; } = new();

    /// <summary>
    /// Sample rows from the CSV for preview
    /// </summary>
    public List<Dictionary<string, string>> SampleRows { get; set; } = new();

    /// <summary>
    /// Overall confidence scores for each mapped field
    /// </summary>
    public Dictionary<string, double> ConfidenceScores { get; set; } = new();

    /// <summary>
    /// Detected bank format (e.g., "Chase Checking", "Generic", "Unknown")
    /// </summary>
    public string DetectedBankFormat { get; set; } = "Unknown";

    /// <summary>
    /// Detected currency from the data
    /// </summary>
    public string? DetectedCurrency { get; set; }

    /// <summary>
    /// List of possible date formats found in the date column
    /// </summary>
    public List<string> DateFormats { get; set; } = new();

    /// <summary>
    /// How amounts are represented (negative-debits, type-column, all-positive)
    /// </summary>
    public string AmountConvention { get; set; } = "unknown";

    /// <summary>
    /// Available CSV column names for mapping
    /// </summary>
    public List<string> AvailableColumns { get; set; } = new();

    /// <summary>
    /// Any warnings or suggestions from the analysis
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Whether the analysis was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if analysis failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Individual column mapping suggestion
/// </summary>
public class ColumnMappingDto
{
    /// <summary>
    /// The CSV column name
    /// </summary>
    public string CsvColumnName { get; set; } = string.Empty;

    /// <summary>
    /// The target field this column maps to (date, amount, description, etc.)
    /// </summary>
    public string TargetField { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score (0.0 - 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Human-readable interpretation of the mapping
    /// </summary>
    public string Interpretation { get; set; } = string.Empty;

    /// <summary>
    /// Sample values from this column
    /// </summary>
    public List<string> SampleValues { get; set; } = new();
}

/// <summary>
/// Column mappings for CSV import
/// </summary>
public class CsvColumnMappings
{
    public string? DateColumn { get; set; }
    public string? AmountColumn { get; set; }
    public string? DescriptionColumn { get; set; }
    public string? TypeColumn { get; set; }
    public string? BalanceColumn { get; set; }
    public string? ReferenceColumn { get; set; }
    public string? CategoryColumn { get; set; }
    
    // Additional mapping configurations
    public string DateFormat { get; set; } = "MM/dd/yyyy";
    public string AmountConvention { get; set; } = "negative-expense";
    
    // Custom type value mappings for income/expense determination
    public TypeValueMappings? TypeValueMappings { get; set; }
}

/// <summary>
/// Custom mappings for type column values to income/expense
/// </summary>
public class TypeValueMappings
{
    public List<string> IncomeValues { get; set; } = new();
    public List<string> ExpenseValues { get; set; } = new();
}

/// <summary>
/// Result of validating CSV mappings
/// </summary>
public class CsvMappingValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public int ValidRowCount { get; set; }
    public int InvalidRowCount { get; set; }
    public List<Dictionary<string, string>> InvalidRows { get; set; } = new();
}