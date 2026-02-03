using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Common.Interfaces;

public interface ILlmCategorizationService
{
    Task<LlmCategorizationResponse> CategorizeTransactionsAsync(
        IEnumerable<Transaction> transactions,
        IEnumerable<Category> categories,
        CancellationToken cancellationToken = default);

    Task<bool> IsServiceAvailableAsync(CancellationToken cancellationToken = default);
    
    Task<string> SendPromptAsync(string prompt, CancellationToken cancellationToken = default);
}

public class LlmCategorizationResponse
{
    public bool Success { get; set; }
    public List<TransactionCategorization> Categorizations { get; set; } = new();
    public CategorizationSummary Summary { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class TransactionCategorization
{
    public int TransactionId { get; set; }
    public List<CategorySuggestion> Suggestions { get; set; } = new();
    public int? RecommendedCategoryId { get; set; }
    public bool RequiresReview { get; set; }
    public SuggestedRule? SuggestedRule { get; set; }
}

public class CategorySuggestion
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public List<int> MatchingRules { get; set; } = new();
}

public class SuggestedRule
{
    public string Pattern { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public decimal Confidence { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class CategorizationSummary
{
    public int TotalProcessed { get; set; }
    public int HighConfidence { get; set; }
    public int MediumConfidence { get; set; }
    public int LowConfidence { get; set; }
    public decimal AverageConfidence { get; set; }
    public int NewRulesGenerated { get; set; }
    public int ProcessingTimeMs { get; set; }
}
