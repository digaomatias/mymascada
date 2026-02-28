namespace MyMascada.Application.Common.Interfaces;

public interface IAiTokenTracker
{
    Task TrackUsageAsync(Guid userId, string model, string operation, int promptTokens, int completionTokens);
    Task<AiUsageSummary> GetUsageSummaryAsync(Guid userId, DateTime from, DateTime to);
    Task<int> GetTotalTokensUsedTodayAsync();
    Task<AiUsageAdminSummary> GetAdminSummaryAsync(DateTime from, DateTime to);
}

public class AiUsageSummary
{
    public int TotalPromptTokens { get; set; }
    public int TotalCompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal TotalEstimatedCostUsd { get; set; }
    public List<AiUsageByOperation> ByOperation { get; set; } = new();
    public List<AiUsageByDay> ByDay { get; set; } = new();
}

public class AiUsageByOperation
{
    public string Operation { get; set; } = string.Empty;
    public int TotalTokens { get; set; }
    public int RequestCount { get; set; }
    public decimal EstimatedCostUsd { get; set; }
}

public class AiUsageByDay
{
    public DateTime Date { get; set; }
    public int TotalTokens { get; set; }
    public int RequestCount { get; set; }
    public decimal EstimatedCostUsd { get; set; }
}

public class AiUsageAdminSummary
{
    public int TotalTokensToday { get; set; }
    public int TotalRequestsToday { get; set; }
    public decimal TotalCostToday { get; set; }
    public List<AiUsageByUser> ByUser { get; set; } = new();
    public List<AiUsageByOperation> ByOperation { get; set; } = new();
    public List<AiUsageByDay> Daily { get; set; } = new();
}

public class AiUsageByUser
{
    public Guid UserId { get; set; }
    public int TotalTokens { get; set; }
    public int RequestCount { get; set; }
    public decimal EstimatedCostUsd { get; set; }
}
