using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.Categorization.Models;

/// <summary>
/// Result of categorization processing pipeline
/// </summary>
public class CategorizationResult
{
    public List<CategorizedTransaction> CategorizedTransactions { get; set; } = new();
    public List<Transaction> RemainingTransactions { get; set; } = new();
    public CategorizationMetrics Metrics { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// High-confidence transactions that should be automatically applied
    /// </summary>
    public List<CategorizedTransaction> AutoAppliedTransactions { get; set; } = new();
    
    /// <summary>
    /// Medium-confidence suggestions that require user approval
    /// </summary>
    public List<CategorizationCandidate> Candidates { get; set; } = new();

    /// <summary>
    /// Merges another result into this one
    /// </summary>
    public CategorizationResult MergeWith(CategorizationResult other)
    {
        CategorizedTransactions.AddRange(other.CategorizedTransactions);
        AutoAppliedTransactions.AddRange(other.AutoAppliedTransactions);
        Candidates.AddRange(other.Candidates);
        RemainingTransactions = other.RemainingTransactions.ToList();
        Metrics.MergeWith(other.Metrics);
        Errors.AddRange(other.Errors);
        return this;
    }
}

/// <summary>
/// A transaction that has been categorized by the pipeline
/// </summary>
public class CategorizedTransaction
{
    public Transaction Transaction { get; set; } = null!;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal ConfidenceScore { get; set; }
    public string ProcessedBy { get; set; } = string.Empty; // "Rules", "ML", "LLM"
    public string Reason { get; set; } = string.Empty;
    public DateTime CategorizedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();

    public CategorizedTransaction() { }

    public CategorizedTransaction(Transaction transaction, int categoryId, string categoryName, decimal confidence, string processedBy, string reason = "")
    {
        Transaction = transaction;
        CategoryId = categoryId;
        CategoryName = categoryName;
        ConfidenceScore = confidence;
        ProcessedBy = processedBy;
        Reason = reason;
    }
}

/// <summary>
/// Metrics collected during categorization processing
/// </summary>
public class CategorizationMetrics
{
    public int TotalTransactions { get; set; }
    public int ProcessedByRules { get; set; }
    public int ProcessedByBankCategory { get; set; }
    public int ProcessedByML { get; set; }
    public int ProcessedByLLM { get; set; }
    public int FailedTransactions { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public decimal EstimatedCostSavings { get; set; }
    public Dictionary<string, int> CategoryDistribution { get; set; } = new();
    public Dictionary<string, decimal> ConfidenceDistribution { get; set; } = new();

    public double SuccessRate => TotalTransactions > 0
        ? (double)(ProcessedByRules + ProcessedByBankCategory + ProcessedByML + ProcessedByLLM) / TotalTransactions
        : 0.0;

    public void MergeWith(CategorizationMetrics other)
    {
        ProcessedByRules += other.ProcessedByRules;
        ProcessedByBankCategory += other.ProcessedByBankCategory;
        ProcessedByML += other.ProcessedByML;
        ProcessedByLLM += other.ProcessedByLLM;
        FailedTransactions += other.FailedTransactions;
        ProcessingTime = ProcessingTime.Add(other.ProcessingTime);
        EstimatedCostSavings += other.EstimatedCostSavings;

        foreach (var category in other.CategoryDistribution)
        {
            CategoryDistribution[category.Key] = CategoryDistribution.GetValueOrDefault(category.Key, 0) + category.Value;
        }

        foreach (var confidence in other.ConfidenceDistribution)
        {
            ConfidenceDistribution[confidence.Key] = ConfidenceDistribution.GetValueOrDefault(confidence.Key, 0) + confidence.Value;
        }
    }
}