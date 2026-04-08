using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RuleSuggestions.DTOs;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.RuleSuggestions.Services;

/// <summary>
/// Service for managing rule suggestions and converting them to categorization rules
/// </summary>
public class RuleSuggestionService : IRuleSuggestionService
{
    private const int MinManuallyCategorizedThreshold = 10;
    private const int MinUncoveredPatternsThreshold = 3;

    private readonly IRuleSuggestionRepository _ruleSuggestionRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategorizationRuleRepository _categorizationRuleRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IRuleSuggestionAnalyzerFactory _analyzerFactory;
    private readonly ICategorizationHistoryRepository _historyRepository;
    private readonly IFeatureFlags _featureFlags;
    private readonly ISubscriptionService _subscriptionService;

    public RuleSuggestionService(
        IRuleSuggestionRepository ruleSuggestionRepository,
        ITransactionRepository transactionRepository,
        ICategorizationRuleRepository categorizationRuleRepository,
        ICategoryRepository categoryRepository,
        IRuleSuggestionAnalyzerFactory analyzerFactory,
        ICategorizationHistoryRepository historyRepository,
        IFeatureFlags featureFlags,
        ISubscriptionService subscriptionService)
    {
        _ruleSuggestionRepository = ruleSuggestionRepository;
        _transactionRepository = transactionRepository;
        _categorizationRuleRepository = categorizationRuleRepository;
        _categoryRepository = categoryRepository;
        _analyzerFactory = analyzerFactory;
        _historyRepository = historyRepository;
        _featureFlags = featureFlags;
        _subscriptionService = subscriptionService;
    }

    /// <summary>
    /// Generates new rule suggestions for a user based on their transaction patterns
    /// </summary>
    public async Task<List<RuleSuggestion>> GenerateSuggestionsAsync(Guid userId, int maxSuggestions = 10, double minConfidence = 0.7, CancellationToken cancellationToken = default)
    {
        // Get required data for analysis
        var recentTransactions = await _transactionRepository.GetRecentTransactionsAsync(userId, 500);
        var transactionsList = recentTransactions.ToList();

        if (transactionsList.Count < 10)
        {
            return new List<RuleSuggestion>(); // Not enough data for meaningful suggestions
        }

        var availableCategories = await _categoryRepository.GetByUserIdAsync(userId);
        var systemCategories = await _categoryRepository.GetSystemCategoriesAsync();
        var allCategories = availableCategories.Concat(systemCategories).ToList();
        
        var existingRules = await _categorizationRuleRepository.GetActiveRulesForUserAsync(userId);

        // Create analysis input
        var analysisInput = new RuleAnalysisInput
        {
            UserId = userId,
            Transactions = transactionsList,
            AvailableCategories = allCategories,
            ExistingRules = existingRules.ToList(),
            MaxSuggestions = maxSuggestions,
            MinConfidenceThreshold = minConfidence
        };

        // Check subscription tier to determine if AI analysis is available
        var canUseAi = (await _subscriptionService.CanUseAiRuleSuggestionsAsync(userId, cancellationToken)).IsAllowed;

        var config = new RuleAnalysisConfiguration
        {
            IsAIAnalysisEnabled = _featureFlags.AiCategorization,
            UseAIForUser = canUseAi,
            FallbackToBasicOnAIFailure = true,
            MaxAICallsPerUserPerDay = 5,
            MinTransactionsForAI = 50
        };

        var analyzer = await _analyzerFactory.CreateAnalyzerAsync(userId, config);

        // Run pattern analysis
        var patternSuggestions = await analyzer.AnalyzePatternsAsync(analysisInput);

        // Convert to domain entities
        var ruleSuggestions = await ConvertPatternSuggestionsToRuleSuggestions(patternSuggestions, userId, analyzer.AnalysisMethod);

        // Filter out duplicates and overlapping suggestions
        var filteredSuggestions = await FilterAndDeduplicateSuggestions(ruleSuggestions, userId, minConfidence);

        // Save suggestions to database, skipping any that fail due to duplicate constraints
        var savedSuggestions = new List<RuleSuggestion>();
        foreach (var suggestion in filteredSuggestions)
        {
            try
            {
                await _ruleSuggestionRepository.CreateSuggestionAsync(suggestion);
                savedSuggestions.Add(suggestion);
            }
            catch (InvalidOperationException)
            {
                // Duplicate detected at DB level (concurrent generation race) — skip
            }
        }

        // Record AI usage if AI-enhanced analyzer was used
        if (canUseAi && analyzer.RequiresAI && savedSuggestions.Count > 0)
        {
            await _subscriptionService.RecordRuleSuggestionUsageAsync(userId, cancellationToken);
        }

        return savedSuggestions;
    }

    /// <summary>
    /// Gets existing rule suggestions for a user
    /// </summary>
    public async Task<List<RuleSuggestion>> GetSuggestionsAsync(Guid userId, bool includeSamples = true)
    {
        var suggestions = await _ruleSuggestionRepository.GetPendingSuggestionsAsync(userId);
        return suggestions.ToList();
    }

    /// <summary>
    /// Accepts a rule suggestion and converts it to an actual categorization rule
    /// </summary>
    public async Task<int> AcceptSuggestionAsync(int suggestionId, Guid userId, string? customName = null, string? customDescription = null, int? priority = null)
    {
        var suggestion = await _ruleSuggestionRepository.GetSuggestionByIdAsync(suggestionId, userId);
        if (suggestion == null)
        {
            throw new ArgumentException($"Rule suggestion {suggestionId} not found for user {userId}");
        }

        if (!suggestion.IsPending)
        {
            throw new InvalidOperationException($"Rule suggestion {suggestionId} has already been processed");
        }

        // Create the categorization rule
        var rule = new CategorizationRule
        {
            Name = customName ?? suggestion.Name,
            Description = customDescription ?? suggestion.Description,
            Type = suggestion.Type,
            Pattern = suggestion.Pattern,
            IsCaseSensitive = suggestion.IsCaseSensitive,
            Priority = priority ?? 0,
            IsActive = true,
            IsAiGenerated = true,
            ConfidenceScore = suggestion.ConfidenceScore,
            UserId = userId,
            CategoryId = suggestion.SuggestedCategoryId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createdRule = await _categorizationRuleRepository.CreateRuleAsync(rule);

        // Mark suggestion as accepted
        suggestion.Accept(createdRule.Id);
        await _ruleSuggestionRepository.UpdateSuggestionAsync(suggestion);

        return createdRule.Id;
    }

    /// <summary>
    /// Rejects/dismisses a rule suggestion
    /// </summary>
    public async Task RejectSuggestionAsync(int suggestionId, Guid userId)
    {
        var suggestion = await _ruleSuggestionRepository.GetSuggestionByIdAsync(suggestionId, userId);
        if (suggestion == null)
        {
            throw new ArgumentException($"Rule suggestion {suggestionId} not found for user {userId}");
        }

        if (!suggestion.IsPending)
        {
            throw new InvalidOperationException($"Rule suggestion {suggestionId} has already been processed");
        }

        suggestion.Reject();
        await _ruleSuggestionRepository.UpdateSuggestionAsync(suggestion);
    }

    /// <summary>
    /// Gets summary statistics for rule suggestions
    /// </summary>
    public async Task<RuleSuggestionsSummaryDto> GetSummaryAsync(Guid userId)
    {
        var (totalSuggestions, averageConfidence, lastGenerated) = await _ruleSuggestionRepository.GetSuggestionStatisticsAsync(userId);
        var suggestions = await _ruleSuggestionRepository.GetPendingSuggestionsAsync(userId);

        // Calculate category distribution
        var categoryDistribution = new Dictionary<string, int>();
        foreach (var suggestion in suggestions)
        {
            var categoryName = suggestion.SuggestedCategory?.Name ?? "Unknown";
            categoryDistribution[categoryName] = categoryDistribution.GetValueOrDefault(categoryName, 0) + 1;
        }

        return new RuleSuggestionsSummaryDto
        {
            TotalSuggestions = totalSuggestions,
            AverageConfidencePercentage = (int)Math.Round(averageConfidence * 100),
            LastGeneratedDate = lastGenerated,
            GenerationMethod = "Basic Pattern Analysis", // Could be dynamic based on last generation
            CategoryDistribution = categoryDistribution
        };
    }

    /// <summary>
    /// Converts pattern suggestions to rule suggestions
    /// </summary>
    private async Task<List<RuleSuggestion>> ConvertPatternSuggestionsToRuleSuggestions(List<PatternSuggestion> patterns, Guid userId, string generationMethod)
    {
        var ruleSuggestions = new List<RuleSuggestion>();

        foreach (var pattern in patterns)
        {
            var suggestion = new RuleSuggestion
            {
                Name = GenerateRuleName(pattern.Pattern, pattern.SuggestedCategoryName),
                Description = pattern.Reasoning ?? $"Found multiple transactions that appear to be from {pattern.SuggestedCategoryName.ToLower()}",
                Pattern = pattern.Pattern,
                Type = pattern.SuggestedRuleType,
                IsCaseSensitive = false,
                ConfidenceScore = pattern.ConfidenceScore,
                MatchCount = pattern.MatchingTransactions.Count,
                GenerationMethod = generationMethod,
                UserId = userId,
                SuggestedCategoryId = pattern.SuggestedCategoryId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Add sample transactions
            var sampleTransactions = pattern.MatchingTransactions
                .OrderByDescending(t => t.TransactionDate)
                .Take(5)
                .ToList();

            foreach (var (transaction, index) in sampleTransactions.Select((t, i) => (t, i)))
            {
                suggestion.AddSampleTransaction(
                    transaction.Id,
                    transaction.Description,
                    transaction.Amount,
                    transaction.TransactionDate,
                    transaction.Account?.Name ?? "Unknown Account"
                );
            }

            ruleSuggestions.Add(suggestion);
        }

        return ruleSuggestions;
    }

    /// <summary>
    /// Filters out low-confidence suggestions and removes duplicates based on transaction overlap
    /// </summary>
    private async Task<List<RuleSuggestion>> FilterAndDeduplicateSuggestions(List<RuleSuggestion> suggestions, Guid userId, double minConfidence)
    {
        var filtered = new List<RuleSuggestion>();
        var existingRules = await _categorizationRuleRepository.GetActiveRulesForUserAsync(userId);

        foreach (var suggestion in suggestions)
        {
            // Skip low-confidence suggestions
            if (suggestion.ConfidenceScore < minConfidence)
                continue;

            // Check for similar existing suggestions
            var similarSuggestions = await _ruleSuggestionRepository.GetSimilarSuggestionsAsync(
                userId, suggestion.Pattern, suggestion.SuggestedCategoryId);

            if (similarSuggestions.Any())
                continue; // Skip if similar suggestion already exists

            // Check if exact rule already exists
            if (existingRules.Any(r => r.Pattern.Equals(suggestion.Pattern, StringComparison.OrdinalIgnoreCase) &&
                                     r.CategoryId == suggestion.SuggestedCategoryId))
                continue; // Skip if rule already exists

            // NEW: Check for transaction overlap with existing rules
            if (await HasSignificantOverlapWithExistingRules(suggestion, existingRules, userId))
            {
                continue; // Skip if transactions are already covered by existing rules
            }

            filtered.Add(suggestion);
        }

        return filtered;
    }

    /// <summary>
    /// Checks if a suggested rule has significant transaction overlap with existing rules
    /// </summary>
    private async Task<bool> HasSignificantOverlapWithExistingRules(RuleSuggestion suggestion, IEnumerable<CategorizationRule> existingRules, Guid userId)
    {
        const double OVERLAP_THRESHOLD = 0.8; // 80% of transactions already covered = redundant

        // Get all transactions that would match this suggested rule pattern
        var potentialMatches = suggestion.SampleTransactions.Select(s => s.TransactionId).ToList();
        
        // For a more thorough check, we could query all user transactions and test the pattern
        // but for now we'll use the sample transactions that were used to create the suggestion
        
        int coveredTransactions = 0;
        
        foreach (var existingRule in existingRules.Where(r => r.IsActive))
        {
            // For each existing rule, check how many of our suggested rule's transactions it would match
            foreach (var sampleTransaction in suggestion.SampleTransactions)
            {
                if (DoesRuleMatchTransaction(existingRule, sampleTransaction.Description))
                {
                    coveredTransactions++;
                }
            }
        }

        // Calculate overlap percentage
        double overlapPercentage = suggestion.SampleTransactions.Any() 
            ? (double)coveredTransactions / suggestion.SampleTransactions.Count 
            : 0;

        return overlapPercentage >= OVERLAP_THRESHOLD;
    }

    /// <summary>
    /// Tests if a rule would match a transaction description.
    /// Delegates to RulePatternMatcher — single source of truth for all rule types including Regex.
    /// </summary>
    private static bool DoesRuleMatchTransaction(CategorizationRule rule, string transactionDescription)
        => RulePatternMatcher.Matches(rule, transactionDescription);

    /// <summary>
    /// Checks trigger conditions: user has enough history entries and enough uncovered patterns.
    /// </summary>
    public async Task<bool> ShouldGenerateRuleSuggestionsAsync(Guid userId, CancellationToken ct = default)
    {
        // Check minimum history entries (proxy for manually categorized transactions)
        var allHistory = await _historyRepository.GetAllForUserAsync(userId, ct);
        if (allHistory.Count < MinManuallyCategorizedThreshold)
            return false;

        // Count history entries not covered by existing rules
        var existingRules = (await _categorizationRuleRepository.GetActiveRulesForUserAsync(userId)).ToList();
        if (!existingRules.Any())
            return true; // No rules and enough history (checked above) = definitely should generate

        int uncoveredCount = 0;
        foreach (var entry in allHistory)
        {
            bool covered = existingRules.Any(r =>
                r.IsActive &&
                r.CategoryId == entry.CategoryId &&
                RulePatternMatcher.Matches(r, entry.OriginalDescription));

            if (!covered)
            {
                uncoveredCount++;
                if (uncoveredCount >= MinUncoveredPatternsThreshold)
                    return true; // Early exit once threshold is met
            }
        }

        return false;
    }

    /// <summary>
    /// Generates a user-friendly name for a rule suggestion
    /// </summary>
    private static string GenerateRuleName(string pattern, string categoryName)
    {
        // Clean up pattern for display
        var cleanPattern = pattern.ToTitleCase();
        
        return pattern.ToUpperInvariant() switch
        {
            "ATM" => "ATM Withdrawals",
            "STARBUCKS" => "Starbucks Coffee Rule",
            "NETFLIX" => "Netflix Subscription Rule",
            "GROCERY" => "Grocery Store Transactions",
            _ => $"{cleanPattern} Transactions"
        };
    }
}

/// <summary>
/// Extension method for title case conversion
/// </summary>
public static class StringExtensions
{
    public static string ToTitleCase(this string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        return string.Join(" ", input.Split(' ')
            .Select(word => char.ToUpper(word[0]) + word[1..].ToLower()));
    }
}
