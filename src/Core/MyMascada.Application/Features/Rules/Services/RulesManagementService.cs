using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace MyMascada.Application.Features.Rules.Services;

/// <summary>
/// Service for managing categorization rules and applying them to transactions
/// Implements the Chain of Responsibility pattern for rule processing
/// </summary>
public class RulesManagementService
{
    private readonly ICategorizationRuleRepository _ruleRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<RulesManagementService> _logger;

    public RulesManagementService(
        ICategorizationRuleRepository ruleRepository,
        ITransactionRepository transactionRepository,
        ILogger<RulesManagementService> logger)
    {
        _ruleRepository = ruleRepository;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    /// <summary>
    /// Applies rules to categorize a single transaction
    /// Returns the suggested category ID and confidence score
    /// </summary>
    public async Task<RuleMatchResult?> ApplyRulesToTransactionAsync(int transactionId, Guid userId, CancellationToken cancellationToken = default)
    {
        var transaction = await _transactionRepository.GetByIdAsync(transactionId);
        if (transaction == null || transaction.Account.UserId != userId)
        {
            _logger.LogWarning("Transaction {TransactionId} not found or does not belong to user {UserId}", transactionId, userId);
            return null;
        }

        var activeRules = await _ruleRepository.GetActiveRulesForUserAsync(userId, cancellationToken);
        var orderedRules = activeRules.OrderBy(r => r.Priority).ToList();

        _logger.LogDebug("Evaluating {RuleCount} rules for transaction {TransactionId}", orderedRules.Count, transactionId);

        foreach (var rule in orderedRules)
        {
            if (rule.Matches(transaction))
            {
                _logger.LogInformation("Rule {RuleId} '{RuleName}' matched transaction {TransactionId}", rule.Id, rule.Name, transactionId);
                
                var confidenceScore = CalculateConfidenceScore(rule, transaction);
                
                return new RuleMatchResult
                {
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    CategoryId = rule.CategoryId,
                    ConfidenceScore = confidenceScore,
                    MatchedConditions = GetMatchedConditions(rule, transaction)
                };
            }
        }

        _logger.LogDebug("No rules matched transaction {TransactionId}", transactionId);
        return null;
    }

    /// <summary>
    /// Applies rules to categorize multiple transactions in batch
    /// </summary>
    public async Task<IEnumerable<TransactionRuleMatch>> ApplyRulesToTransactionsAsync(IEnumerable<int> transactionIds, Guid userId, CancellationToken cancellationToken = default)
    {
        var results = new List<TransactionRuleMatch>();
        var activeRules = await _ruleRepository.GetActiveRulesForUserAsync(userId, cancellationToken);
        var orderedRules = activeRules.OrderBy(r => r.Priority).ToList();

        foreach (var transactionId in transactionIds)
        {
            var transaction = await _transactionRepository.GetByIdAsync(transactionId);
            if (transaction == null || transaction.Account.UserId != userId)
            {
                continue;
            }

            var matchResult = await ApplyRulesToTransactionAsync(transactionId, userId, cancellationToken);
            results.Add(new TransactionRuleMatch
            {
                TransactionId = transactionId,
                MatchResult = matchResult
            });
        }

        return results;
    }

    /// <summary>
    /// Records that a rule was applied to a transaction
    /// </summary>
    public async Task<RuleApplication> RecordRuleApplicationAsync(int ruleId, int transactionId, int categoryId, Guid userId, string triggerSource = "Automatic", CancellationToken cancellationToken = default)
    {
        var rule = await _ruleRepository.GetRuleByIdAsync(ruleId, userId, cancellationToken);
        if (rule == null)
        {
            throw new ArgumentException($"Rule {ruleId} not found or does not belong to user {userId}");
        }

        var confidenceScore = rule.ConfidenceScore?.ToString() ?? "0.8";
        var application = rule.RecordApplication(transactionId, categoryId, decimal.Parse(confidenceScore), triggerSource);
        
        _ = await _ruleRepository.UpdateRuleAsync(rule, cancellationToken);
        
        _logger.LogInformation("Recorded application of rule {RuleId} to transaction {TransactionId} with category {CategoryId}", 
            ruleId, transactionId, categoryId);

        return application;
    }

    /// <summary>
    /// Records that a rule application was corrected by the user
    /// </summary>
    public async Task RecordRuleCorrectionAsync(int ruleId, int transactionId, int newCategoryId, Guid userId, CancellationToken cancellationToken = default)
    {
        var rule = await _ruleRepository.GetRuleByIdAsync(ruleId, userId, cancellationToken);
        if (rule == null)
        {
            throw new ArgumentException($"Rule {ruleId} not found or does not belong to user {userId}");
        }

        // Find the application and mark it as corrected
        var application = rule.Applications.FirstOrDefault(a => a.TransactionId == transactionId);
        if (application != null)
        {
            application.RecordCorrection(newCategoryId);
        }

        // Update rule correction statistics
        rule.RecordCorrection();
        
        _ = await _ruleRepository.UpdateRuleAsync(rule, cancellationToken);
        
        _logger.LogInformation("Recorded correction for rule {RuleId} on transaction {TransactionId}, new category: {NewCategoryId}", 
            ruleId, transactionId, newCategoryId);
    }

    /// <summary>
    /// Tests a rule against existing transactions to show potential matches
    /// </summary>
    public async Task<RuleTestResult> TestRuleAsync(CategorizationRule rule, Guid userId, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        var recentTransactions = await _transactionRepository.GetRecentTransactionsAsync(userId, maxResults * 2); // Get more to account for matches
        var matches = new List<Transaction>();
        var processedCount = 0;

        foreach (var transaction in recentTransactions.Take(maxResults * 2))
        {
            processedCount++;
            if (rule.Matches(transaction))
            {
                matches.Add(transaction);
                if (matches.Count >= maxResults)
                    break;
            }
        }

        return new RuleTestResult
        {
            TotalTransactionsEvaluated = processedCount,
            MatchingTransactions = matches,
            MatchCount = matches.Count,
            EstimatedAccuracy = CalculateEstimatedAccuracy(rule, matches)
        };
    }

    /// <summary>
    /// Suggests new rules based on user's transaction patterns
    /// </summary>
    public async Task<IEnumerable<RuleSuggestion>> SuggestRulesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var suggestions = new List<RuleSuggestion>();
        
        // Get recent transactions that have been categorized
        var categorizedTransactions = await _transactionRepository.GetCategorizedTransactionsAsync(userId, 200);
        
        // Group by category and look for patterns
        var categoryGroups = categorizedTransactions.GroupBy(t => t.CategoryId);
        
        foreach (var group in categoryGroups)
        {
            if (group.Count() < 3) continue; // Need at least 3 transactions to suggest a rule
            
            var commonPatterns = FindCommonPatterns(group.ToList());
            foreach (var pattern in commonPatterns)
            {
                suggestions.Add(new RuleSuggestion
                {
                    Name = $"Auto-categorize {pattern.Description}",
                    Description = $"Automatically categorize transactions matching '{pattern.Pattern}' as {group.First().Category?.Name}",
                    CategoryId = group.Key ?? 0,
                    Pattern = pattern.Pattern,
                    RuleType = RuleType.Contains,
                    Confidence = pattern.Confidence,
                    SampleTransactionCount = pattern.SampleCount
                });
            }
        }
        
        return suggestions.OrderByDescending(s => s.Confidence).Take(10);
    }

    private decimal CalculateConfidenceScore(CategorizationRule rule, Transaction transaction)
    {
        var baseScore = rule.ConfidenceScore ?? 0.8;
        var accuracyRate = rule.GetAccuracyRate();
        
        // Adjust confidence based on rule performance
        var adjustedScore = (decimal)accuracyRate * (decimal)baseScore;
        
        // Boost confidence for exact matches
        if (rule.Type == RuleType.Equals && rule.MatchesDescription(transaction.Description))
        {
            adjustedScore = Math.Min(1.0m, adjustedScore * 1.2m);
        }
        
        return Math.Max(0.1m, Math.Min(1.0m, adjustedScore));
    }

    private List<string> GetMatchedConditions(CategorizationRule rule, Transaction transaction)
    {
        var conditions = new List<string>();
        
        if (rule.HasAdvancedConditions())
        {
            foreach (var condition in rule.Conditions.Where(c => !c.IsDeleted))
            {
                if (condition.Evaluate(transaction))
                {
                    conditions.Add($"{condition.Field}: {condition.Operator} '{condition.Value}'");
                }
            }
        }
        else
        {
            // Legacy pattern matching
            if (rule.MatchesDescription(transaction.Description))
            {
                conditions.Add($"Description {rule.Type} '{rule.Pattern}'");
            }
        }
        
        return conditions;
    }

    private double CalculateEstimatedAccuracy(CategorizationRule rule, List<Transaction> matches)
    {
        if (!matches.Any()) return 0.0;
        
        // For new rules, estimate based on pattern specificity
        if (rule.MatchCount == 0)
        {
            return rule.Type switch
            {
                RuleType.Equals => 0.95,
                RuleType.StartsWith => 0.85,
                RuleType.EndsWith => 0.85,
                RuleType.Contains => 0.75,
                RuleType.Regex => 0.80,
                _ => 0.70
            };
        }
        
        return rule.GetAccuracyRate();
    }

    private List<PatternMatch> FindCommonPatterns(List<Transaction> transactions)
    {
        var patterns = new List<PatternMatch>();
        
        // Look for common words in descriptions
        var words = transactions
            .SelectMany(t => t.Description.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(w => w.Length > 3) // Ignore short words
            .GroupBy(w => w.ToLowerInvariant())
            .Where(g => g.Count() >= Math.Max(2, transactions.Count * 0.6)) // Must appear in at least 60% of transactions
            .OrderByDescending(g => g.Count());
        
        foreach (var word in words.Take(3)) // Top 3 patterns
        {
            patterns.Add(new PatternMatch
            {
                Pattern = word.Key,
                Description = $"transactions containing '{word.Key}'",
                Confidence = (double)word.Count() / transactions.Count,
                SampleCount = word.Count()
            });
        }
        
        return patterns;
    }
}

/// <summary>
/// Result of applying rules to a transaction
/// </summary>
public class RuleMatchResult
{
    public int RuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public decimal ConfidenceScore { get; set; }
    public List<string> MatchedConditions { get; set; } = new();
}

/// <summary>
/// Result of applying rules to a transaction with transaction ID
/// </summary>
public class TransactionRuleMatch
{
    public int TransactionId { get; set; }
    public RuleMatchResult? MatchResult { get; set; }
}

/// <summary>
/// Result of testing a rule against transactions
/// </summary>
public class RuleTestResult
{
    public int TotalTransactionsEvaluated { get; set; }
    public List<Transaction> MatchingTransactions { get; set; } = new();
    public int MatchCount { get; set; }
    public double EstimatedAccuracy { get; set; }
}

/// <summary>
/// Suggestion for a new rule based on transaction patterns
/// </summary>
public class RuleSuggestion
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public RuleType RuleType { get; set; }
    public double Confidence { get; set; }
    public int SampleTransactionCount { get; set; }
}

/// <summary>
/// Pattern match found in transaction analysis
/// </summary>
public class PatternMatch
{
    public string Pattern { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public int SampleCount { get; set; }
}