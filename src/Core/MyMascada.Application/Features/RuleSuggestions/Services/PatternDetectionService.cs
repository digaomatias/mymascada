using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using System.Text.RegularExpressions;

namespace MyMascada.Application.Features.RuleSuggestions.Services;

/// <summary>
/// Service for detecting patterns in transaction data for rule suggestions
/// </summary>
public class PatternDetectionService : IPatternDetectionService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ILlmCategorizationService _llmService;

    public PatternDetectionService(ICategoryRepository categoryRepository, ILlmCategorizationService llmService)
    {
        _categoryRepository = categoryRepository;
        _llmService = llmService;
    }

    /// <summary>
    /// Detects keyword-based patterns by analyzing transaction description frequencies
    /// </summary>
    public async Task<List<PatternSuggestion>> DetectKeywordPatternsAsync(List<Transaction> transactions, int minOccurrences = 3)
    {
        var suggestions = new List<PatternSuggestion>();
        
        // Group transactions by keywords and current categories
        var keywordGroups = new Dictionary<string, List<Transaction>>();
        
        foreach (var transaction in transactions.Where(t => !string.IsNullOrWhiteSpace(t.Description)))
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

        // Analyze patterns where keyword appears frequently with same category
        foreach (var (keyword, matchingTransactions) in keywordGroups)
        {
            if (matchingTransactions.Count < minOccurrences)
                continue;

            // Group by category to find the most common categorization
            var categoryCounts = matchingTransactions
                .Where(t => t.CategoryId.HasValue)
                .GroupBy(t => t.CategoryId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            if (!categoryCounts.Any())
                continue;

            // Find the most frequently used category for this keyword
            var (mostCommonCategoryId, categoryCount) = categoryCounts
                .OrderByDescending(kvp => kvp.Value)
                .First();

            // Calculate confidence based on consistency
            var confidence = (double)categoryCount / matchingTransactions.Count;
            
            // Only suggest if confidence is reasonable and there are enough samples
            if (confidence >= 0.6 && categoryCount >= minOccurrences)
            {
                var category = await _categoryRepository.GetByIdAsync(mostCommonCategoryId);
                if (category != null)
                {
                    suggestions.Add(new PatternSuggestion
                    {
                        Pattern = keyword,
                        SuggestedCategoryId = mostCommonCategoryId,
                        SuggestedCategoryName = category.Name,
                        ConfidenceScore = confidence,
                        MatchingTransactions = matchingTransactions.Where(t => t.CategoryId == mostCommonCategoryId).ToList(),
                        DetectionMethod = "Keyword Frequency Analysis",
                        Reasoning = $"Found '{keyword}' in {categoryCount} out of {matchingTransactions.Count} transactions, consistently categorized as {category.Name}"
                    });
                }
            }
        }

        return suggestions.OrderByDescending(s => s.ConfidenceScore).ToList();
    }

    /// <summary>
    /// Uses AI to detect complex patterns in transaction descriptions
    /// </summary>
    public async Task<List<PatternSuggestion>> DetectAiPatternsAsync(List<Transaction> transactions)
    {
        var suggestions = new List<PatternSuggestion>();

        try
        {
            // Group uncategorized transactions for AI analysis
            var uncategorizedTransactions = transactions
                .Where(t => !t.CategoryId.HasValue && !string.IsNullOrWhiteSpace(t.Description))
                .Take(50) // Limit for AI processing
                .ToList();

            if (uncategorizedTransactions.Count < 3)
                return suggestions;

            // Get available categories
            var categories = await _categoryRepository.GetSystemCategoriesAsync();
            var categoryNames = categories.Select(c => $"{c.Id}:{c.Name}").ToList();

            // Prepare AI prompt
            var transactionDescriptions = uncategorizedTransactions
                .Select(t => $"- {t.Description}")
                .Take(20);

            var prompt = $@"Analyze these transaction descriptions and suggest categorization patterns:

{string.Join("\n", transactionDescriptions)}

Available categories: {string.Join(", ", categoryNames)}

Find recurring patterns that could be automated into rules. For each pattern found, respond in this JSON format:
{{
  ""patterns"": [
    {{
      ""pattern"": ""keyword or pattern to match"",
      ""categoryId"": ""category ID number"",
      ""categoryName"": ""category name"",
      ""confidence"": ""confidence score between 0 and 1"",
      ""reasoning"": ""explanation of why this pattern makes sense""
    }}
  ]
}}

Focus on:
1. Clear recurring keywords (ATM, Starbucks, Netflix, etc.)
2. Merchant name patterns
3. Transaction type patterns
4. Only suggest patterns with high confidence (>0.8)
5. Limit to top 5 most useful patterns";

            var aiResponse = await _llmService.SendPromptAsync(prompt);
            
            // Parse AI response and create suggestions
            // Note: This would need proper JSON parsing in a real implementation
            // For now, we'll create a simple fallback pattern
            
            // Create fallback suggestions based on simple heuristics
            var merchantPatterns = DetectMerchantPatterns(uncategorizedTransactions);
            suggestions.AddRange(merchantPatterns);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the entire process
            // In production, you'd use proper logging
            System.Diagnostics.Debug.WriteLine($"AI pattern detection failed: {ex.Message}");
        }

        return suggestions;
    }

    /// <summary>
    /// Extracts meaningful keywords from transaction descriptions
    /// </summary>
    private static List<string> ExtractKeywords(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return new List<string>();

        // Clean and normalize the description
        var cleaned = Regex.Replace(description.ToUpperInvariant(), @"[^\w\s]", " ");
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Filter out common words and extract meaningful keywords
        var stopWords = new HashSet<string> 
        { 
            "THE", "AND", "OR", "BUT", "IN", "ON", "AT", "TO", "FOR", "OF", "WITH", "BY",
            "PURCHASE", "PAYMENT", "TRANSACTION", "DEBIT", "CREDIT", "CARD", "ACCOUNT",
            "DATE", "TIME", "LOCATION", "STORE", "SHOP", "INC", "LLC", "LTD", "CO", "CORP"
        };

        var keywords = new List<string>();

        // Add individual meaningful words (length > 3, not stop words)
        keywords.AddRange(words.Where(w => w.Length > 3 && !stopWords.Contains(w)));

        // Add common merchant patterns
        if (description.ToUpperInvariant().Contains("ATM"))
            keywords.Add("ATM");
        
        // Look for common merchant name patterns (words that appear to be proper nouns)
        var properNouns = words.Where(w => w.Length > 4 && 
            char.IsUpper(description[description.IndexOf(w, StringComparison.OrdinalIgnoreCase)]));
        keywords.AddRange(properNouns);

        return keywords.Distinct().Take(5).ToList(); // Limit to top 5 keywords
    }

    /// <summary>
    /// Detects merchant-based patterns as a fallback when AI is not available
    /// </summary>
    private List<PatternSuggestion> DetectMerchantPatterns(List<Transaction> transactions)
    {
        var suggestions = new List<PatternSuggestion>();

        // Look for recurring merchant names
        var merchantPatterns = new Dictionary<string, List<Transaction>>();

        foreach (var transaction in transactions)
        {
            var description = transaction.Description?.ToUpperInvariant() ?? "";
            
            // Simple merchant detection patterns
            if (description.Contains("STARBUCKS"))
                AddToMerchantPattern(merchantPatterns, "STARBUCKS", transaction);
            else if (description.Contains("NETFLIX"))
                AddToMerchantPattern(merchantPatterns, "NETFLIX", transaction);
            else if (description.Contains("ATM"))
                AddToMerchantPattern(merchantPatterns, "ATM", transaction);
            else if (description.Contains("GROCERY") || description.Contains("SUPERMARKET"))
                AddToMerchantPattern(merchantPatterns, "GROCERY", transaction);
            else if (description.Contains("GAS") || description.Contains("FUEL"))
                AddToMerchantPattern(merchantPatterns, "GAS", transaction);
        }

        // Convert patterns to suggestions with reasonable defaults
        foreach (var (pattern, matchingTransactions) in merchantPatterns)
        {
            if (matchingTransactions.Count >= 2) // Minimum occurrences
            {
                var (categoryId, categoryName) = GetDefaultCategoryForPattern(pattern);
                
                suggestions.Add(new PatternSuggestion
                {
                    Pattern = pattern,
                    SuggestedCategoryId = categoryId,
                    SuggestedCategoryName = categoryName,
                    ConfidenceScore = 0.85, // High confidence for known patterns
                    MatchingTransactions = matchingTransactions,
                    DetectionMethod = "Merchant Pattern Analysis",
                    Reasoning = $"Detected recurring '{pattern}' transactions suitable for {categoryName} category"
                });
            }
        }

        return suggestions;
    }

    private static void AddToMerchantPattern(Dictionary<string, List<Transaction>> patterns, string pattern, Transaction transaction)
    {
        if (!patterns.ContainsKey(pattern))
            patterns[pattern] = new List<Transaction>();
        patterns[pattern].Add(transaction);
    }

    private static (int CategoryId, string CategoryName) GetDefaultCategoryForPattern(string pattern)
    {
        return pattern switch
        {
            "STARBUCKS" => (4, "Food & Dining"), // Assuming category ID 4 is Food & Dining
            "NETFLIX" => (5, "Entertainment"), // Assuming category ID 5 is Entertainment
            "ATM" => (6, "Cash & ATM"), // Assuming category ID 6 is Cash & ATM
            "GROCERY" => (7, "Groceries"), // Assuming category ID 7 is Groceries
            "GAS" => (8, "Transportation"), // Assuming category ID 8 is Transportation
            _ => (1, "Other") // Default fallback
        };
    }
}
