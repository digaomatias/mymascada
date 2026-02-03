using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.RuleSuggestions.DTOs;

/// <summary>
/// DTO for rule suggestion data transfer
/// </summary>
public class RuleSuggestionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public RuleType Type { get; set; }
    public bool IsCaseSensitive { get; set; }
    public double ConfidenceScore { get; set; }
    public int ConfidencePercentage => (int)Math.Round(ConfidenceScore * 100);
    public int MatchCount { get; set; }
    public string GenerationMethod { get; set; } = string.Empty;
    public int SuggestedCategoryId { get; set; }
    public string SuggestedCategoryName { get; set; } = string.Empty;
    public string? SuggestedCategoryColor { get; set; }
    public string? SuggestedCategoryIcon { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<RuleSuggestionSampleDto> SampleTransactions { get; set; } = new();
}

/// <summary>
/// DTO for sample transaction data
/// </summary>
public class RuleSuggestionSampleDto
{
    public int TransactionId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>
/// DTO for rule suggestions summary statistics
/// </summary>
public class RuleSuggestionsSummaryDto
{
    public int TotalSuggestions { get; set; }
    public int AverageConfidencePercentage { get; set; }
    public DateTime? LastGeneratedDate { get; set; }
    public string GenerationMethod { get; set; } = string.Empty;
    public Dictionary<string, int> CategoryDistribution { get; set; } = new();
}

/// <summary>
/// Response DTO containing suggestions and summary
/// </summary>
public class RuleSuggestionsResponse
{
    public RuleSuggestionsSummaryDto Summary { get; set; } = new();
    public List<RuleSuggestionDto> Suggestions { get; set; } = new();
}

/// <summary>
/// Request DTO for accepting a rule suggestion
/// </summary>
public class AcceptRuleSuggestionRequest
{
    public int SuggestionId { get; set; }
    public string? RuleName { get; set; }
    public string? RuleDescription { get; set; }
    public int? Priority { get; set; }
}

/// <summary>
/// Request DTO for generating new rule suggestions
/// </summary>
public class GenerateRuleSuggestionsRequest
{
    public Guid UserId { get; set; }
    public int? LimitSuggestions { get; set; } = 10;
    public double? MinConfidenceThreshold { get; set; } = 0.7;
    public bool ForceRegenerate { get; set; } = false;
}