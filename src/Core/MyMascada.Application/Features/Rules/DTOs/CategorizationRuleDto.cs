using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.Rules.DTOs;

public class CategorizationRuleDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public RuleType Type { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public bool IsCaseSensitive { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public bool IsAiGenerated { get; set; }
    public double? ConfidenceScore { get; set; }
    public int MatchCount { get; set; }
    public int CorrectionCount { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public string? AccountTypes { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public RuleLogic Logic { get; set; }
    public double AccuracyRate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<RuleConditionDto> Conditions { get; set; } = new();
    public int ApplicationCount { get; set; }
}

public class RuleConditionDto
{
    public int Id { get; set; }
    public RuleConditionField Field { get; set; }
    public RuleConditionOperator Operator { get; set; }
    public string Value { get; set; } = string.Empty;
    public bool IsCaseSensitive { get; set; }
    public int Order { get; set; }
}

public class RuleMatchResultDto
{
    public int RuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public decimal ConfidenceScore { get; set; }
    public List<string> MatchedConditions { get; set; } = new();
}

public class TransactionRuleMatchDto
{
    public int TransactionId { get; set; }
    public RuleMatchResultDto? MatchResult { get; set; }
}

public class RuleTestResultDto
{
    public int RuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public int TotalMatches { get; set; }
    public List<MatchingTransactionDto> MatchingTransactions { get; set; } = new();
    public DateTime TestedAt { get; set; }
    public string TestSummary { get; set; } = string.Empty;
}

public class MatchingTransactionDto
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string? CurrentCategoryName { get; set; }
    public string? SuggestedCategoryName { get; set; }
    public bool WouldChangeCategory { get; set; }
    
    // For frontend compatibility - returns current category name
    public string? CategoryName => CurrentCategoryName;
}

public class TransactionSummaryDto
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
}

public class RuleSuggestionDto
{
    public string SuggestedName { get; set; } = string.Empty;
    public string SuggestedPattern { get; set; } = string.Empty;
    public string SuggestedType { get; set; } = "Contains";
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public int MatchingTransactionCount { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public List<SampleTransactionDto> SampleTransactions { get; set; } = new();
    public Dictionary<string, object> AdditionalProperties { get; set; } = new();
}

public class SampleTransactionDto
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string? CurrentCategoryName { get; set; }
}

public class RuleSuggestionsResponse
{
    public List<RuleSuggestionDto> Suggestions { get; set; } = new();
    public int TotalSuggestions { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string AnalysisMethod { get; set; } = string.Empty;
    public Dictionary<string, int> CategoryDistribution { get; set; } = new();
}

public class RuleStatisticsDto
{
    public int TotalRules { get; set; }
    public int ActiveRules { get; set; }
    public int TotalApplications { get; set; }
    public int TotalCorrections { get; set; }
    public double OverallAccuracy { get; set; }
    public List<RulePerformanceDto> TopPerformingRules { get; set; } = new();
    public List<RulePerformanceDto> PoorPerformingRules { get; set; } = new();
}

public class RulePerformanceDto
{
    public int RuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public int MatchCount { get; set; }
    public int CorrectionCount { get; set; }
    public double AccuracyRate { get; set; }
    public DateTime LastUsed { get; set; }
}