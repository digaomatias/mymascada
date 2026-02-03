using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Features.RuleSuggestions.Services;

namespace MyMascada.Infrastructure.Services.RuleSuggestions;

/// <summary>
/// Service for tracking AI usage to control costs and enforce quotas
/// </summary>
public class AIUsageTracker : IAIUsageTracker
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AIUsageTracker> _logger;

    // Configuration keys
    private const string MaxCallsPerDayKey = "RuleSuggestions:MaxAICallsPerUserPerDay";
    private const string DefaultMaxCallsPerDay = "5"; // Conservative default

    public AIUsageTracker(
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<AIUsageTracker> logger)
    {
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> CanUseAIAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var maxCalls = GetMaxCallsPerDay();
        var currentUsage = await GetCurrentUsageAsync(userId);
        
        var canUse = currentUsage < maxCalls;
        
        if (!canUse)
        {
            _logger.LogWarning("AI usage quota exceeded for user {UserId}. Usage: {CurrentUsage}/{MaxCalls}", 
                userId, currentUsage, maxCalls);
        }
        else
        {
            _logger.LogDebug("AI usage check for user {UserId}. Usage: {CurrentUsage}/{MaxCalls}", 
                userId, currentUsage, maxCalls);
        }

        return canUse;
    }

    public async Task RecordAIUsageAsync(Guid userId, string operation, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetDailyUsageCacheKey(userId);
        var currentUsage = await GetCurrentUsageAsync(userId);
        var newUsage = currentUsage + 1;

        // Cache until end of day
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = DateTime.Today.AddDays(1),
            Priority = CacheItemPriority.High
        };

        _cache.Set(cacheKey, newUsage, cacheOptions);

        _logger.LogInformation("Recorded AI usage for user {UserId}. Operation: {Operation}, New Usage: {Usage}", 
            userId, operation, newUsage);

        // Optional: Also record to persistent storage for analytics
        await RecordToPersistentStorageAsync(userId, operation, newUsage);
    }

    public async Task<int> GetRemainingQuotaAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var maxCalls = GetMaxCallsPerDay();
        var currentUsage = await GetCurrentUsageAsync(userId);
        var remaining = Math.Max(0, maxCalls - currentUsage);

        _logger.LogDebug("Remaining AI quota for user {UserId}: {Remaining}", userId, remaining);
        
        return remaining;
    }

    /// <summary>
    /// Gets current usage count from cache
    /// </summary>
    private async Task<int> GetCurrentUsageAsync(Guid userId)
    {
        var cacheKey = GetDailyUsageCacheKey(userId);
        
        if (_cache.TryGetValue(cacheKey, out int usage))
        {
            return usage;
        }

        // If not in cache, try to load from persistent storage
        var persistentUsage = await LoadUsageFromPersistentStorageAsync(userId);
        
        // Cache the loaded value
        if (persistentUsage > 0)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = DateTime.Today.AddDays(1),
                Priority = CacheItemPriority.High
            };
            _cache.Set(cacheKey, persistentUsage, cacheOptions);
        }

        return persistentUsage;
    }

    /// <summary>
    /// Gets the maximum allowed AI calls per day from configuration
    /// </summary>
    private int GetMaxCallsPerDay()
    {
        var maxCallsString = _configuration[MaxCallsPerDayKey] ?? DefaultMaxCallsPerDay;
        
        if (int.TryParse(maxCallsString, out int maxCalls) && maxCalls > 0)
        {
            return maxCalls;
        }

        _logger.LogWarning("Invalid configuration for {ConfigKey}, using default value {DefaultValue}", 
            MaxCallsPerDayKey, DefaultMaxCallsPerDay);
            
        return int.Parse(DefaultMaxCallsPerDay);
    }

    /// <summary>
    /// Generates cache key for daily usage tracking
    /// </summary>
    private static string GetDailyUsageCacheKey(Guid userId)
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        return $"ai_usage:{userId}:{today}";
    }

    /// <summary>
    /// Records usage to persistent storage for analytics and audit
    /// </summary>
    private async Task RecordToPersistentStorageAsync(Guid userId, string operation, int newUsage)
    {
        try
        {
            // In a real implementation, you might:
            // 1. Store in database for analytics
            // 2. Send to logging service
            // 3. Update user statistics
            
            // For now, just log it
            _logger.LogInformation("AI usage recorded - User: {UserId}, Operation: {Operation}, Daily Total: {Usage}", 
                userId, operation, newUsage);
            
            // TODO: Implement actual persistent storage
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record AI usage to persistent storage for user {UserId}", userId);
            // Don't throw - this shouldn't break the main flow
        }
    }

    /// <summary>
    /// Loads usage from persistent storage (e.g., database)
    /// </summary>
    private async Task<int> LoadUsageFromPersistentStorageAsync(Guid userId)
    {
        try
        {
            // In a real implementation, you would:
            // 1. Query database for today's usage
            // 2. Return the count
            
            // For now, return 0 (no persistent storage yet)
            await Task.CompletedTask;
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load AI usage from persistent storage for user {UserId}", userId);
            return 0; // Safe fallback
        }
    }
}

/// <summary>
/// Configuration options for AI usage tracking
/// </summary>
public class AIUsageConfiguration
{
    public const string SectionName = "RuleSuggestions";

    /// <summary>
    /// Maximum AI calls per user per day
    /// </summary>
    public int MaxAICallsPerUserPerDay { get; set; } = 5;

    /// <summary>
    /// Whether to enable AI usage tracking
    /// </summary>
    public bool EnableUsageTracking { get; set; } = true;

    /// <summary>
    /// Whether to store usage data persistently
    /// </summary>
    public bool UsePersistentStorage { get; set; } = false;
}