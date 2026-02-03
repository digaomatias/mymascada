using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyMascada.Application.Common.Configuration;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Models;
using MyMascada.Domain.Entities;
using System.Text.Json;

namespace MyMascada.Application.Features.Categorization.Handlers;

/// <summary>
/// First handler in the chain - applies user-defined categorization rules
/// Fastest processing with zero cost
/// </summary>
public class RulesHandler : CategorizationHandler
{
    private readonly ICategorizationRuleRepository _ruleRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly CategorizationOptions _options;

    public RulesHandler(
        ICategorizationRuleRepository ruleRepository,
        ICategoryRepository categoryRepository,
        IOptions<CategorizationOptions> options,
        ILogger<RulesHandler> logger) : base(logger)
    {
        _ruleRepository = ruleRepository;
        _categoryRepository = categoryRepository;
        _options = options.Value;
    }

    public override string HandlerType => "Rules";

    protected override async Task<CategorizationResult> ProcessTransactionsAsync(
        IEnumerable<Transaction> transactions, 
        CancellationToken cancellationToken)
    {
        var result = new CategorizationResult();
        var transactionsList = transactions.ToList();

        if (!transactionsList.Any())
            return result;

        // Get user ID from first transaction (all transactions should belong to same user in batch)
        var firstTransaction = transactionsList.First();
        _logger.LogInformation("RulesHandler: Processing {TransactionCount} transactions. First transaction: ID={TransactionId}, Description='{Description}', Account={Account}", 
            transactionsList.Count, firstTransaction.Id, firstTransaction.Description, firstTransaction.Account?.Name ?? "NULL");
        
        var userId = firstTransaction.Account?.UserId;
        if (userId == null)
        {
            _logger.LogWarning("RulesHandler: Cannot process rules - no user ID found in transactions. Account is null: {AccountIsNull}", firstTransaction.Account == null);
            return result;
        }
        
        _logger.LogInformation("RulesHandler: Processing rules for user {UserId}", userId);

        // Get active rules for the user, ordered by priority
        var activeRules = await _ruleRepository.GetActiveRulesForUserAsync(userId.Value, cancellationToken);
        var orderedRules = activeRules.OrderBy(r => r.Priority).ToList();

        if (!orderedRules.Any())
        {
            _logger.LogWarning("RulesHandler: No active rules found for user {UserId}", userId);
            return result;
        }

        _logger.LogInformation("RulesHandler: Found {RuleCount} active rules for user {UserId}. Rules: {RuleNames}", 
            orderedRules.Count, userId, string.Join(", ", orderedRules.Select(r => $"{r.Id}:{r.Name}")));
        
        foreach (var rule in orderedRules)
        {
            _logger.LogInformation("RulesHandler: Rule {RuleId} '{RuleName}' - Pattern: '{Pattern}', Type: {Type}, Active: {IsActive}", 
                rule.Id, rule.Name, rule.Pattern, rule.Type, rule.IsActive);
        }

        foreach (var transaction in transactionsList)
        {
            _logger.LogInformation("RulesHandler: Testing transaction {TransactionId} '{Description}' against {RuleCount} rules", 
                transaction.Id, transaction.Description, orderedRules.Count);
            
            var matchResult = await FindBestRuleMatchForTransaction(transaction, orderedRules, cancellationToken);
            if (matchResult != null)
            {
                if (matchResult.CanAutoApply)
                {
                    // High confidence - add to auto-apply list
                    if (matchResult.CategorizedTransaction != null)
                    {
                        result.AutoAppliedTransactions.Add(matchResult.CategorizedTransaction);
                        _logger.LogInformation("RulesHandler: Rule {RuleId} matched transaction {TransactionId} with high confidence {Confidence} - will auto-apply",
                            matchResult.RuleId, transaction.Id, matchResult.Confidence);
                    }
                }
                else
                {
                    // Lower confidence - add to candidates list for user review
                    if (matchResult.Candidate != null)
                    {
                        result.Candidates.Add(matchResult.Candidate);
                        _logger.LogInformation("RulesHandler: Rule matched transaction {TransactionId} with confidence {Confidence} - created candidate for review",
                            transaction.Id, matchResult.Confidence);
                    }
                }
            }
            else
            {
                _logger.LogInformation("RulesHandler: No rule matched transaction {TransactionId} '{Description}'", 
                    transaction.Id, transaction.Description);
            }
        }

        // Update result with categorized transactions for metrics tracking
        // Include both auto-applied and candidates (candidates were processed, just need review)
        result.CategorizedTransactions = result.AutoAppliedTransactions.ToList();

        // Also track transactions that have candidates as "categorized" (pending review)
        // This ensures they're not passed to the next handler in the chain
        foreach (var candidate in result.Candidates)
        {
            var transaction = transactionsList.FirstOrDefault(t => t.Id == candidate.TransactionId);
            if (transaction != null && candidate.Category != null)
            {
                result.CategorizedTransactions.Add(CreateCategorizedTransaction(
                    transaction,
                    candidate.CategoryId,
                    candidate.Category.Name,
                    candidate.ConfidenceScore,
                    candidate.Reasoning ?? "",
                    null));
            }
        }

        UpdateMetrics(result, result.AutoAppliedTransactions.Count);
        // Override ProcessedByRules to include both auto-applied and candidates
        result.Metrics.ProcessedByRules = result.AutoAppliedTransactions.Count + result.Candidates.Count;

        // Update category distribution metrics
        foreach (var categorized in result.AutoAppliedTransactions)
        {
            result.Metrics.CategoryDistribution[categorized.CategoryName] = 
                result.Metrics.CategoryDistribution.GetValueOrDefault(categorized.CategoryName, 0) + 1;
            
            var confidenceRange = GetConfidenceRange(categorized.ConfidenceScore);
            result.Metrics.ConfidenceDistribution[confidenceRange] = 
                result.Metrics.ConfidenceDistribution.GetValueOrDefault(confidenceRange, 0) + 1;
        }
        
        _logger.LogInformation("RulesHandler completed: {AutoAppliedCount} auto-applied, {CandidateCount} candidates created",
            result.AutoAppliedTransactions.Count, result.Candidates.Count);

        return result;
    }

    private async Task<RuleMatchResult?> FindBestRuleMatchForTransaction(
        Transaction transaction, 
        IEnumerable<CategorizationRule> rules, 
        CancellationToken cancellationToken)
    {
        foreach (var rule in rules)
        {
            try
            {
                _logger.LogInformation("RulesHandler: Testing rule {RuleId} '{RuleName}' (Pattern: '{Pattern}', Type: {Type}) against transaction '{Description}'", 
                    rule.Id, rule.Name, rule.Pattern, rule.Type, transaction.Description);
                
                if (rule.Matches(transaction))
                {
                    // Skip rules with missing categories (deleted or invalid category reference)
                    if (rule.Category == null)
                    {
                        _logger.LogWarning("RulesHandler: Rule {RuleId} '{RuleName}' matched but has no category - skipping",
                            rule.Id, rule.Name);
                        continue;
                    }

                    _logger.LogInformation("RulesHandler: 🎯 RULE MATCH! Rule {RuleId} '{RuleName}' matched transaction '{Description}'",
                        rule.Id, rule.Name, transaction.Description);

                    var confidenceScore = CalculateConfidenceScore(rule, transaction);
                    var reason = BuildMatchReason(rule, transaction);
                    var canAutoApply = confidenceScore >= _options.AutoApplyConfidenceThreshold;

                    var metadata = new Dictionary<string, object>
                    {
                        ["RuleId"] = rule.Id,
                        ["RuleName"] = rule.Name,
                        ["RulePattern"] = rule.Pattern,
                        ["RuleType"] = rule.Type.ToString(),
                        ["Priority"] = rule.Priority,
                        ["MatchedAt"] = DateTime.UtcNow
                    };

                    if (canAutoApply)
                    {
                        // Create CategorizedTransaction for immediate application
                        var categorizedTransaction = CreateCategorizedTransaction(
                            transaction,
                            rule.CategoryId,
                            rule.Category.Name,
                            confidenceScore,
                            reason,
                            metadata);

                        return new RuleMatchResult
                        {
                            CanAutoApply = true,
                            CategorizedTransaction = categorizedTransaction,
                            RuleId = rule.Id,
                            Confidence = confidenceScore
                        };
                    }

                    // Create CategorizationCandidate for user review
                    var candidate = new CategorizationCandidate
                    {
                        TransactionId = transaction.Id,
                        CategoryId = rule.CategoryId,
                        Category = rule.Category, // Set the navigation property!
                        CategorizationMethod = CandidateMethod.Rule,
                        ConfidenceScore = confidenceScore,
                        ProcessedBy = "RulesHandler",
                        Reasoning = reason,
                        Metadata = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        }),
                        Status = CandidateStatus.Pending,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedBy = $"RulesHandler-{transaction.Account.UserId}",
                        UpdatedBy = $"RulesHandler-{transaction.Account.UserId}"
                    };

                    return new RuleMatchResult
                    {
                        CanAutoApply = false,
                        Candidate = candidate,
                        RuleId = rule.Id,
                        Confidence = confidenceScore
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying rule {RuleId} to transaction {TransactionId}", 
                    rule.Id, transaction.Id);
            }
        }

        return null; // No matching rule found
    }


    private decimal CalculateConfidenceScore(CategorizationRule rule, Transaction transaction)
    {
        var baseScore = rule.ConfidenceScore ?? 0.8;
        var accuracyRate = rule.GetAccuracyRate();
        
        // Adjust confidence based on rule performance and type
        var adjustedScore = (decimal)accuracyRate * (decimal)baseScore;
        
        // Boost confidence for exact matches
        if (rule.Type == Domain.Enums.RuleType.Equals && 
            string.Equals(rule.Pattern, transaction.Description, 
                rule.IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
        {
            adjustedScore = Math.Min(1.0m, adjustedScore * 1.2m);
        }
        
        // Smart boost for Contains rules that effectively match the entire description
        // E.g., "Netflix" rule matching "Netflix Monthly Subscription" vs "Netflix" transaction
        if (rule.Type == Domain.Enums.RuleType.Contains && 
            !string.IsNullOrEmpty(rule.Pattern))
        {
            var comparison = rule.IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            
            // Check if pattern matches the entire description (exact match)
            if (string.Equals(rule.Pattern.Trim(), transaction.Description.Trim(), comparison))
            {
                // Perfect match - boost to 100% confidence
                adjustedScore = 1.0m;
            }
            // Check if pattern is a significant portion of the description
            else if (rule.Pattern.Length >= 4 && // Only for meaningful patterns
                     transaction.Description.Contains(rule.Pattern, comparison))
            {
                var patternRatio = (decimal)rule.Pattern.Length / transaction.Description.Length;
                
                // If pattern represents 60%+ of the description, boost confidence significantly
                if (patternRatio >= 0.6m)
                {
                    adjustedScore = Math.Min(1.0m, adjustedScore * 1.15m);
                }
                // If pattern represents 40%+ of the description, boost moderately  
                else if (patternRatio >= 0.4m)
                {
                    adjustedScore = Math.Min(1.0m, adjustedScore * 1.1m);
                }
            }
        }
        
        // Reduce confidence for very broad patterns
        if (rule.Type == Domain.Enums.RuleType.Contains && 
            !string.IsNullOrEmpty(rule.Pattern) && rule.Pattern.Length < 3)
        {
            adjustedScore *= 0.8m;
        }
        
        return Math.Max(0.1m, Math.Min(1.0m, adjustedScore));
    }

    private string BuildMatchReason(CategorizationRule rule, Transaction transaction)
    {
        var reason = $"Matched rule '{rule.Name}'";
        
        if (!string.IsNullOrEmpty(rule.Pattern))
        {
            reason += $" (pattern: '{rule.Pattern}')";
        }
        
        if (rule.HasAdvancedConditions())
        {
            var matchedConditions = GetMatchedConditions(rule, transaction);
            if (matchedConditions.Any())
            {
                reason += $" with conditions: {string.Join(", ", matchedConditions)}";
            }
        }
        
        return reason;
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
                    conditions.Add($"{condition.Field} {condition.Operator} '{condition.Value}'");
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


    private static string GetConfidenceRange(decimal confidence)
    {
        return confidence switch
        {
            >= 0.9m => "High (90-100%)",
            >= 0.7m => "Medium (70-89%)",
            >= 0.5m => "Low (50-69%)",
            _ => "Very Low (<50%)"
        };
    }
}

/// <summary>
/// Result of applying a rule to a transaction
/// Contains either a categorized transaction for auto-apply or a candidate for user review
/// </summary>
internal class RuleMatchResult
{
    public bool CanAutoApply { get; set; }
    public CategorizedTransaction? CategorizedTransaction { get; set; }
    public CategorizationCandidate? Candidate { get; set; }
    public int RuleId { get; set; }
    public decimal Confidence { get; set; }
}