namespace MyMascada.Application.Common.Configuration;

/// <summary>
/// Configuration options for the categorization pipeline
/// </summary>
public class CategorizationOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Categorization";

    /// <summary>
    /// Confidence threshold for auto-applying rule-based categorizations (default: 95%)
    /// Rules with confidence >= this threshold will be auto-applied instead of creating candidates
    /// </summary>
    public decimal AutoApplyConfidenceThreshold { get; set; } = 0.95m;

    /// <summary>
    /// Confidence threshold for auto-applying ML-based categorizations (default: 95%)
    /// </summary>
    public decimal MLAutoApplyThreshold { get; set; } = 0.95m;

    /// <summary>
    /// Confidence threshold for auto-applying LLM-based categorizations (default: 90%)
    /// </summary>
    public decimal LLMAutoApplyThreshold { get; set; } = 0.90m;

    /// <summary>
    /// Maximum number of transactions to process in a single pipeline batch (default: 500)
    /// </summary>
    public int MaxBatchSize { get; set; } = 500;

    /// <summary>
    /// Whether to enable ML handler in the pipeline (default: false)
    /// </summary>
    public bool EnableMLHandler { get; set; } = false;

    /// <summary>
    /// Whether to enable LLM handler in the pipeline (default: true)
    /// </summary>
    public bool EnableLLMHandler { get; set; } = true;
}