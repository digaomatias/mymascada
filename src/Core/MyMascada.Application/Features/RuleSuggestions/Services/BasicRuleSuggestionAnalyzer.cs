using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using System.Text.RegularExpressions;

namespace MyMascada.Application.Features.RuleSuggestions.Services;

/// <summary>
/// Basic rule suggestion analyzer that uses keyword frequency and pattern matching without AI
/// </summary>
public class BasicRuleSuggestionAnalyzer : IRuleSuggestionAnalyzer
{
    private readonly ICategoryRepository _categoryRepository;

    public string AnalysisMethod => "Basic Pattern Analysis";
    public bool RequiresAI => false;

    public BasicRuleSuggestionAnalyzer(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<List<PatternSuggestion>> AnalyzePatternsAsync(RuleAnalysisInput input, CancellationToken cancellationToken = default)
    {
        var suggestions = new List<PatternSuggestion>();

        // 1. Keyword frequency analysis
        var keywordSuggestions = await AnalyzeKeywordFrequency(input);
        suggestions.AddRange(keywordSuggestions);

        // 2. Merchant pattern analysis
        var merchantSuggestions = await AnalyzeMerchantPatterns(input);
        suggestions.AddRange(merchantSuggestions);

        // 3. Amount pattern analysis
        var amountSuggestions = await AnalyzeAmountPatterns(input);
        suggestions.AddRange(amountSuggestions);

        // 4. Date pattern analysis
        var dateSuggestions = await AnalyzeDatePatterns(input);
        suggestions.AddRange(dateSuggestions);

        // Filter and rank suggestions
        return FilterAndRankSuggestions(suggestions, input.MaxSuggestions, input.MinConfidenceThreshold);
    }

    /// <summary>
    /// Analyzes keyword frequency in transaction descriptions
    /// </summary>
    private async Task<List<PatternSuggestion>> AnalyzeKeywordFrequency(RuleAnalysisInput input)
    {
        var suggestions = new List<PatternSuggestion>();
        var keywordGroups = new Dictionary<string, List<Transaction>>();

        // Extract keywords from each transaction
        foreach (var transaction in input.Transactions.Where(t => !string.IsNullOrWhiteSpace(t.Description)))
        {
            var keywords = ExtractKeywords(transaction.Description);
            
            foreach (var keyword in keywords)
            {
                if (!keywordGroups.ContainsKey(keyword))
                {
                    keywordGroups[keyword] = new List<Transaction>();
                }
                keywordGroups[keyword].Add(transaction);
            }
        }

        // Analyze patterns for each keyword
        foreach (var (keyword, transactions) in keywordGroups)
        {
            if (transactions.Count < 3) // Minimum occurrences
                continue;

            // Find the most common category for this keyword
            var categoryCounts = transactions
                .Where(t => t.CategoryId.HasValue)
                .GroupBy(t => t.CategoryId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            if (!categoryCounts.Any())
                continue;

            var (mostCommonCategoryId, categoryCount) = categoryCounts
                .OrderByDescending(kvp => kvp.Value)
                .First();

            // Calculate confidence based on consistency
            var confidence = (double)categoryCount / transactions.Count;
            
            if (confidence >= 0.6 && categoryCount >= 3)
            {
                var category = input.AvailableCategories.FirstOrDefault(c => c.Id == mostCommonCategoryId);
                if (category != null)
                {
                    suggestions.Add(new PatternSuggestion
                    {
                        Pattern = keyword,
                        SuggestedCategoryId = mostCommonCategoryId,
                        SuggestedCategoryName = category.Name,
                        ConfidenceScore = confidence,
                        MatchingTransactions = transactions.Where(t => t.CategoryId == mostCommonCategoryId).ToList(),
                        DetectionMethod = "Keyword Frequency Analysis",
                        Reasoning = $"Found '{keyword}' in {categoryCount} out of {transactions.Count} transactions, consistently categorized as {category.Name}"
                    });
                }
            }
        }

        return suggestions;
    }

    /// <summary>
    /// Analyzes common merchant patterns
    /// </summary>
    private async Task<List<PatternSuggestion>> AnalyzeMerchantPatterns(RuleAnalysisInput input)
    {
        var suggestions = new List<PatternSuggestion>();
        var merchantPatterns = new Dictionary<string, List<Transaction>>();

        // Define common merchant patterns
        var patterns = new Dictionary<string, (string Pattern, Regex Regex, int CategoryId, string CategoryName)>
        {
            ["ATM"] = ("ATM", new Regex(@"\bATM\b", RegexOptions.IgnoreCase), GetCategoryId("Cash & ATM", input.AvailableCategories), "Cash & ATM"),
            ["STARBUCKS"] = ("STARBUCKS", new Regex(@"\bSTARBUCKS\b", RegexOptions.IgnoreCase), GetCategoryId("Food & Dining", input.AvailableCategories), "Food & Dining"),
            ["NETFLIX"] = ("NETFLIX", new Regex(@"\bNETFLIX\b", RegexOptions.IgnoreCase), GetCategoryId("Entertainment", input.AvailableCategories), "Entertainment"),
            ["GROCERY"] = ("GROCERY", new Regex(@"\b(GROCERY|SUPERMARKET|GROCERIES)\b", RegexOptions.IgnoreCase), GetCategoryId("Groceries", input.AvailableCategories), "Groceries"),
            ["GAS"] = ("GAS", new Regex(@"\b(GAS|FUEL|PETROL)\b", RegexOptions.IgnoreCase), GetCategoryId("Transportation", input.AvailableCategories), "Transportation"),
            ["AMAZON"] = ("AMAZON", new Regex(@"\bAMAZON\b", RegexOptions.IgnoreCase), GetCategoryId("Shopping", input.AvailableCategories), "Shopping"),
            ["PAYPAL"] = ("PAYPAL", new Regex(@"\bPAYPAL\b", RegexOptions.IgnoreCase), GetCategoryId("Transfer", input.AvailableCategories), "Transfer")
        };

        // Find matching transactions for each pattern
        foreach (var (patternName, (pattern, regex, categoryId, categoryName)) in patterns)
        {
            if (categoryId == 0) continue; // Category not found

            var matchingTransactions = input.Transactions
                .Where(t => !string.IsNullOrWhiteSpace(t.Description) && regex.IsMatch(t.Description))
                .ToList();

            if (matchingTransactions.Count >= 2)
            {
                // Check if this pattern doesn't already exist as a rule
                var existingRule = input.ExistingRules.Any(r => 
                    r.Pattern.Equals(pattern, StringComparison.OrdinalIgnoreCase) && 
                    r.CategoryId == categoryId);

                if (!existingRule)
                {
                    suggestions.Add(new PatternSuggestion
                    {
                        Pattern = pattern,
                        SuggestedCategoryId = categoryId,
                        SuggestedCategoryName = categoryName,
                        ConfidenceScore = 0.85, // High confidence for known merchant patterns
                        MatchingTransactions = matchingTransactions,
                        DetectionMethod = "Merchant Pattern Analysis",
                        Reasoning = $"Detected recurring '{pattern}' transactions suitable for {categoryName} category"
                    });
                }
            }
        }

        return suggestions;
    }

    /// <summary>
    /// Analyzes amount-based patterns (e.g., recurring payments)
    /// </summary>
    private async Task<List<PatternSuggestion>> AnalyzeAmountPatterns(RuleAnalysisInput input)
    {
        var suggestions = new List<PatternSuggestion>();

        // Group transactions by exact amount to find recurring payments
        var amountGroups = input.Transactions
            .Where(t => !string.IsNullOrWhiteSpace(t.Description))
            .GroupBy(t => Math.Abs(t.Amount))
            .Where(g => g.Count() >= 3) // At least 3 transactions with same amount
            .ToList();

        foreach (var amountGroup in amountGroups)
        {
            var transactions = amountGroup.ToList();
            var amount = amountGroup.Key;

            // Look for consistent descriptions with same amount (likely subscriptions)
            var descriptionGroups = transactions
                .GroupBy(t => NormalizeDescription(t.Description))
                .Where(g => g.Count() >= 3)
                .ToList();

            foreach (var descGroup in descriptionGroups)
            {
                var sameDescTransactions = descGroup.ToList();
                var normalizedDesc = descGroup.Key;

                // Check for consistent categorization
                var categorizedTransactions = sameDescTransactions.Where(t => t.CategoryId.HasValue).ToList();
                if (categorizedTransactions.Count >= 2)
                {
                    var mostCommonCategory = categorizedTransactions
                        .GroupBy(t => t.CategoryId!.Value)
                        .OrderByDescending(g => g.Count())
                        .First();

                    var consistency = (double)mostCommonCategory.Count() / categorizedTransactions.Count;
                    
                    if (consistency >= 0.8) // High consistency required for amount-based patterns
                    {
                        var category = input.AvailableCategories.FirstOrDefault(c => c.Id == mostCommonCategory.Key);
                        if (category != null)
                        {
                            // Create a pattern based on key words from the description
                            var pattern = ExtractMainKeyword(normalizedDesc);
                            
                            suggestions.Add(new PatternSuggestion
                            {
                                Pattern = pattern,
                                SuggestedCategoryId = mostCommonCategory.Key,
                                SuggestedCategoryName = category.Name,
                                ConfidenceScore = consistency * 0.9, // Slightly lower confidence than exact matches
                                MatchingTransactions = sameDescTransactions,
                                DetectionMethod = "Recurring Amount Analysis",
                                Reasoning = $"Found recurring ${amount:F2} payments to '{pattern}' consistently categorized as {category.Name}"
                            });
                        }
                    }
                }
            }
        }

        return suggestions;
    }

    /// <summary>
    /// Analyzes date-based patterns (e.g., monthly subscriptions)
    /// </summary>
    private async Task<List<PatternSuggestion>> AnalyzeDatePatterns(RuleAnalysisInput input)
    {
        var suggestions = new List<PatternSuggestion>();

        // Group transactions by description to find recurring patterns
        var descriptionGroups = input.Transactions
            .Where(t => !string.IsNullOrWhiteSpace(t.Description))
            .GroupBy(t => NormalizeDescription(t.Description))
            .Where(g => g.Count() >= 3)
            .ToList();

        foreach (var group in descriptionGroups)
        {
            var transactions = group.OrderBy(t => t.TransactionDate).ToList();
            var intervals = new List<int>();

            // Calculate intervals between transactions
            for (int i = 1; i < transactions.Count; i++)
            {
                var interval = (transactions[i].TransactionDate - transactions[i-1].TransactionDate).Days;
                if (interval > 0 && interval <= 90) // Reasonable range for recurring payments
                {
                    intervals.Add(interval);
                }
            }

            if (intervals.Count >= 2)
            {
                // Check if intervals are consistent (monthly ~30 days, weekly ~7 days, etc.)
                var avgInterval = intervals.Average();
                var isMonthly = Math.Abs(avgInterval - 30) <= 5; // ~monthly
                var isWeekly = Math.Abs(avgInterval - 7) <= 2;   // ~weekly

                if (isMonthly || isWeekly)
                {
                    // Check for consistent categorization
                    var categorizedTransactions = transactions.Where(t => t.CategoryId.HasValue).ToList();
                    if (categorizedTransactions.Count >= 2)
                    {
                        var mostCommonCategory = categorizedTransactions
                            .GroupBy(t => t.CategoryId!.Value)
                            .OrderByDescending(g => g.Count())
                            .First();

                        var consistency = (double)mostCommonCategory.Count() / categorizedTransactions.Count;
                        
                        if (consistency >= 0.75)
                        {
                            var category = input.AvailableCategories.FirstOrDefault(c => c.Id == mostCommonCategory.Key);
                            if (category != null)
                            {
                                var pattern = ExtractMainKeyword(group.Key);
                                var frequency = isMonthly ? "monthly" : "weekly";
                                
                                suggestions.Add(new PatternSuggestion
                                {
                                    Pattern = pattern,
                                    SuggestedCategoryId = mostCommonCategory.Key,
                                    SuggestedCategoryName = category.Name,
                                    ConfidenceScore = consistency * 0.8, // Moderate confidence for date patterns
                                    MatchingTransactions = transactions,
                                    DetectionMethod = "Recurring Date Analysis",
                                    Reasoning = $"Found {frequency} recurring payments to '{pattern}' consistently categorized as {category.Name}"
                                });
                            }
                        }
                    }
                }
            }
        }

        return suggestions;
    }

    /// <summary>
    /// Filters and ranks suggestions by confidence and relevance, removing overlapping suggestions
    /// </summary>
    private List<PatternSuggestion> FilterAndRankSuggestions(List<PatternSuggestion> suggestions, int maxSuggestions, double minConfidence)
    {
        var filteredSuggestions = suggestions
            .Where(s => s.ConfidenceScore >= minConfidence)
            .OrderByDescending(s => s.ConfidenceScore)
            .ThenByDescending(s => s.MatchingTransactions.Count)
            .ToList();

        // Remove overlapping suggestions (suggestions that share >50% of transactions)
        var finalSuggestions = new List<PatternSuggestion>();
        
        foreach (var suggestion in filteredSuggestions)
        {
            bool hasSignificantOverlap = false;
            
            foreach (var existing in finalSuggestions)
            {
                var overlapCount = suggestion.MatchingTransactions
                    .Intersect(existing.MatchingTransactions, new TransactionIdComparer())
                    .Count();
                
                var minTransactionCount = Math.Min(suggestion.MatchingTransactions.Count, existing.MatchingTransactions.Count);
                var overlapPercentage = minTransactionCount > 0 ? (double)overlapCount / minTransactionCount : 0;
                
                // If more than 70% overlap, consider it a duplicate
                if (overlapPercentage > 0.7)
                {
                    hasSignificantOverlap = true;
                    break;
                }
            }
            
            if (!hasSignificantOverlap)
            {
                finalSuggestions.Add(suggestion);
            }
            
            if (finalSuggestions.Count >= maxSuggestions)
                break;
        }

        return finalSuggestions;
    }

    /// <summary>
    /// Comparer for transactions based on ID
    /// </summary>
    private class TransactionIdComparer : IEqualityComparer<Transaction>
    {
        public bool Equals(Transaction? x, Transaction? y)
        {
            if (x == null || y == null) return false;
            return x.Id == y.Id;
        }

        public int GetHashCode(Transaction obj)
        {
            return obj?.Id.GetHashCode() ?? 0;
        }
    }

    /// <summary>
    /// Extracts meaningful keywords from transaction descriptions
    /// </summary>
    private static List<string> ExtractKeywords(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return new List<string>();

        var cleaned = Regex.Replace(description.ToUpperInvariant(), @"[^\w\s]", " ");
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var stopWords = new HashSet<string> 
        { 
            "THE", "AND", "OR", "BUT", "IN", "ON", "AT", "TO", "FOR", "OF", "WITH", "BY",
            "PURCHASE", "PAYMENT", "TRANSACTION", "DEBIT", "CREDIT", "CARD", "ACCOUNT",
            "DATE", "TIME", "LOCATION", "STORE", "SHOP", "INC", "LLC", "LTD", "CO", "CORP"
        };

        return words
            .Where(w => w.Length > 3 && !stopWords.Contains(w))
            .Distinct()
            .Take(5)
            .ToList();
    }

    /// <summary>
    /// Normalizes description for pattern matching
    /// </summary>
    private static string NormalizeDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return string.Empty;

        // Remove common variations (dates, numbers, etc.)
        var normalized = Regex.Replace(description.ToUpperInvariant(), @"\d+", "X");
        normalized = Regex.Replace(normalized, @"[^\w\s]", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        return normalized;
    }

    /// <summary>
    /// Extracts the main keyword from a normalized description
    /// </summary>
    private static string ExtractMainKeyword(string normalizedDescription)
    {
        var words = normalizedDescription.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.FirstOrDefault(w => w.Length > 3 && w != "X") ?? normalizedDescription.Split(' ').FirstOrDefault() ?? "UNKNOWN";
    }

    /// <summary>
    /// Gets category ID by name with fallback
    /// </summary>
    private static int GetCategoryId(string categoryName, List<Category> availableCategories)
    {
        var category = availableCategories.FirstOrDefault(c => 
            c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
        return category?.Id ?? 0;
    }
}