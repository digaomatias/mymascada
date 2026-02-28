using MyMascada.Domain.Common;

namespace MyMascada.Domain.Entities;

public class AiTokenUsage : BaseEntity<int>
{
    public Guid UserId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Model { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty; // chat, categorization, csv-import, rule-suggestion
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
}
