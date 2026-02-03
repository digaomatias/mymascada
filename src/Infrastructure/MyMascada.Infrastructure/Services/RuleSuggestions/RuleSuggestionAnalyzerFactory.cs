using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyMascada.Application.Features.RuleSuggestions.Services;

namespace MyMascada.Infrastructure.Services.RuleSuggestions;

/// <summary>
/// Factory for creating appropriate rule suggestion analyzers based on configuration
/// </summary>
public class RuleSuggestionAnalyzerFactory : IRuleSuggestionAnalyzerFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RuleSuggestionAnalyzerFactory> _logger;
    private readonly IAIUsageTracker _usageTracker;

    public RuleSuggestionAnalyzerFactory(
        IServiceProvider serviceProvider,
        ILogger<RuleSuggestionAnalyzerFactory> logger,
        IAIUsageTracker usageTracker)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _usageTracker = usageTracker;
    }

    public async Task<IRuleSuggestionAnalyzer> CreateAnalyzerAsync(Guid userId, RuleAnalysisConfiguration config)
    {
        try
        {
            // Check if AI should be used for this user
            var shouldUseAI = await ShouldUseAIAnalysis(userId, config);

            if (shouldUseAI)
            {
                _logger.LogInformation("Creating AI-enhanced analyzer for user {UserId}", userId);
                return _serviceProvider.GetRequiredService<AIEnhancedRuleSuggestionAnalyzer>();
            }
            else
            {
                _logger.LogInformation("Creating basic analyzer for user {UserId}", userId);
                return _serviceProvider.GetRequiredService<BasicRuleSuggestionAnalyzer>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating analyzer for user {UserId}, falling back to basic analyzer", userId);
            return _serviceProvider.GetRequiredService<BasicRuleSuggestionAnalyzer>();
        }
    }

    /// <summary>
    /// Determines if AI analysis should be used based on configuration and user context
    /// </summary>
    private async Task<bool> ShouldUseAIAnalysis(Guid userId, RuleAnalysisConfiguration config)
    {
        // Check global AI enablement
        if (!config.IsAIAnalysisEnabled)
        {
            _logger.LogDebug("AI analysis disabled globally for user {UserId}", userId);
            return false;
        }

        // Check user-specific AI enablement (for pro/free tier)
        if (!config.UseAIForUser)
        {
            _logger.LogDebug("AI analysis disabled for user {UserId} (user tier restriction)", userId);
            return false;
        }

        // Check AI usage quota
        var canUseAI = await _usageTracker.CanUseAIAsync(userId);
        if (!canUseAI)
        {
            _logger.LogDebug("AI usage quota exceeded for user {UserId}", userId);
            return false;
        }

        // Check if AI service is available (optional health check)
        try
        {
            var aiService = _serviceProvider.GetService<AIEnhancedRuleSuggestionAnalyzer>();
            if (aiService == null)
            {
                _logger.LogWarning("AI analyzer service not available for user {UserId}", userId);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve AI analyzer service for user {UserId}", userId);
            return false;
        }

        return true;
    }
}
