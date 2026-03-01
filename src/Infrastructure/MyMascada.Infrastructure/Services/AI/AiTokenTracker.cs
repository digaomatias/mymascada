using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;

namespace MyMascada.Infrastructure.Services.AI;

public class AiTokenTracker : IAiTokenTracker
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<AiTokenTracker> _logger;

    public AiTokenTracker(ApplicationDbContext dbContext, ILogger<AiTokenTracker> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task TrackUsageAsync(Guid userId, string model, string operation, int promptTokens, int completionTokens)
    {
        try
        {
            var totalTokens = promptTokens + completionTokens;
            var estimatedCost = CalculateEstimatedCost(model, promptTokens, completionTokens);

            var usage = new AiTokenUsage
            {
                UserId = userId,
                Timestamp = DateTimeProvider.UtcNow,
                Model = model,
                Operation = operation,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = totalTokens,
                EstimatedCostUsd = estimatedCost
            };

            _dbContext.AiTokenUsages.Add(usage);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "AI token usage tracked - User: {UserId}, Operation: {Operation}, Model: {Model}, Tokens: {TotalTokens}, Cost: ${Cost:F6}",
                userId, operation, model, totalTokens, estimatedCost);
        }
        catch (Exception ex)
        {
            // Don't let tracking failures break the main flow
            _logger.LogError(ex, "Failed to track AI token usage for user {UserId}, operation {Operation}", userId, operation);
        }
    }

    public async Task<AiUsageSummary> GetUsageSummaryAsync(Guid userId, DateTime from, DateTime to)
    {
        var usages = await _dbContext.AiTokenUsages
            .Where(u => u.UserId == userId && u.Timestamp >= from && u.Timestamp <= to)
            .ToListAsync();

        return new AiUsageSummary
        {
            TotalPromptTokens = usages.Sum(u => u.PromptTokens),
            TotalCompletionTokens = usages.Sum(u => u.CompletionTokens),
            TotalTokens = usages.Sum(u => u.TotalTokens),
            TotalEstimatedCostUsd = usages.Sum(u => u.EstimatedCostUsd),
            ByOperation = usages
                .GroupBy(u => u.Operation)
                .Select(g => new AiUsageByOperation
                {
                    Operation = g.Key,
                    TotalTokens = g.Sum(u => u.TotalTokens),
                    RequestCount = g.Count(),
                    EstimatedCostUsd = g.Sum(u => u.EstimatedCostUsd)
                })
                .OrderByDescending(o => o.TotalTokens)
                .ToList(),
            ByDay = usages
                .GroupBy(u => u.Timestamp.Date)
                .Select(g => new AiUsageByDay
                {
                    Date = g.Key,
                    TotalTokens = g.Sum(u => u.TotalTokens),
                    RequestCount = g.Count(),
                    EstimatedCostUsd = g.Sum(u => u.EstimatedCostUsd)
                })
                .OrderBy(d => d.Date)
                .ToList()
        };
    }

    public async Task<int> GetTotalTokensUsedTodayAsync()
    {
        var todayUtc = DateTimeProvider.UtcNow.Date;
        return await _dbContext.AiTokenUsages
            .Where(u => u.Timestamp >= todayUtc)
            .SumAsync(u => u.TotalTokens);
    }

    public async Task<AiUsageAdminSummary> GetAdminSummaryAsync(DateTime from, DateTime to)
    {
        var usages = await _dbContext.AiTokenUsages
            .Where(u => u.Timestamp >= from && u.Timestamp <= to)
            .ToListAsync();

        var todayUtc = DateTimeProvider.UtcNow.Date;
        var todayUsages = usages.Where(u => u.Timestamp >= todayUtc).ToList();

        return new AiUsageAdminSummary
        {
            TotalTokensToday = todayUsages.Sum(u => u.TotalTokens),
            TotalRequestsToday = todayUsages.Count,
            TotalCostToday = todayUsages.Sum(u => u.EstimatedCostUsd),
            ByUser = usages
                .GroupBy(u => u.UserId)
                .Select(g => new AiUsageByUser
                {
                    UserId = g.Key,
                    TotalTokens = g.Sum(u => u.TotalTokens),
                    RequestCount = g.Count(),
                    EstimatedCostUsd = g.Sum(u => u.EstimatedCostUsd)
                })
                .OrderByDescending(u => u.TotalTokens)
                .ToList(),
            ByOperation = usages
                .GroupBy(u => u.Operation)
                .Select(g => new AiUsageByOperation
                {
                    Operation = g.Key,
                    TotalTokens = g.Sum(u => u.TotalTokens),
                    RequestCount = g.Count(),
                    EstimatedCostUsd = g.Sum(u => u.EstimatedCostUsd)
                })
                .OrderByDescending(o => o.TotalTokens)
                .ToList(),
            Daily = usages
                .GroupBy(u => u.Timestamp.Date)
                .Select(g => new AiUsageByDay
                {
                    Date = g.Key,
                    TotalTokens = g.Sum(u => u.TotalTokens),
                    RequestCount = g.Count(),
                    EstimatedCostUsd = g.Sum(u => u.EstimatedCostUsd)
                })
                .OrderBy(d => d.Date)
                .ToList()
        };
    }

    private static decimal CalculateEstimatedCost(string model, int promptTokens, int completionTokens)
    {
        // Pricing per 1M tokens (as of 2025)
        var (inputPricePerMillion, outputPricePerMillion) = model.ToLowerInvariant() switch
        {
            var m when m.Contains("gpt-4o-mini") => (0.15m, 0.60m),
            var m when m.Contains("gpt-4o") => (2.50m, 10.00m),
            var m when m.Contains("gpt-4-turbo") => (10.00m, 30.00m),
            var m when m.Contains("gpt-4") => (30.00m, 60.00m),
            var m when m.Contains("gpt-3.5") => (0.50m, 1.50m),
            _ => (0.15m, 0.60m) // Default to gpt-4o-mini pricing
        };

        return (promptTokens * inputPricePerMillion / 1_000_000m)
             + (completionTokens * outputPricePerMillion / 1_000_000m);
    }
}
