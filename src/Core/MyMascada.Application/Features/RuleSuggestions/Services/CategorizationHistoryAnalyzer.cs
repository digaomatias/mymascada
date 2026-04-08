using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.RuleSuggestions.Services;

/// <summary>
/// Analyzes a user's categorization history to find clusters of similar descriptions
/// and generates rule suggestions. Uses deterministic common-token detection first;
/// clusters without an obvious pattern are batched for AI analysis.
/// </summary>
public class CategorizationHistoryAnalyzer : ICategorizationHistoryAnalyzer
{
    private const int MinClusterSize = 3;
    private const int MinTokenLength = 4;
    private const double MinPrecision = 0.90;
    private const double MinRecall = 0.60;
    private const double DeterministicConfidence = 0.85;

    private readonly ICategorizationHistoryRepository _historyRepository;
    private readonly ICategorizationRuleRepository _ruleRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ILogger<CategorizationHistoryAnalyzer> _logger;

    public CategorizationHistoryAnalyzer(
        ICategorizationHistoryRepository historyRepository,
        ICategorizationRuleRepository ruleRepository,
        ICategoryRepository categoryRepository,
        ILogger<CategorizationHistoryAnalyzer> logger)
    {
        _historyRepository = historyRepository;
        _ruleRepository = ruleRepository;
        _categoryRepository = categoryRepository;
        _logger = logger;
    }

    public async Task<HistoryAnalysisResult> AnalyzeAsync(Guid userId, CancellationToken ct = default)
    {
        var result = new HistoryAnalysisResult();

        var allHistory = await _historyRepository.GetAllForUserAsync(userId, ct);
        result.TotalEntriesAnalyzed = allHistory.Count;

        if (allHistory.Count < MinClusterSize)
        {
            _logger.LogDebug("User {UserId} has only {Count} history entries — skipping analysis",
                userId, allHistory.Count);
            return result;
        }

        var existingRules = (await _ruleRepository.GetActiveRulesForUserAsync(userId)).ToList();

        // Group history by category
        var categoryGroups = allHistory
            .GroupBy(h => h.CategoryId)
            .Where(g => g.Count() >= MinClusterSize);

        // Load categories for name lookup
        var userCategories = await _categoryRepository.GetByUserIdAsync(userId);
        var systemCategories = await _categoryRepository.GetSystemCategoriesAsync();
        var allCategories = userCategories.Concat(systemCategories)
            .ToDictionary(c => c.Id, c => c.Name);

        foreach (var categoryGroup in categoryGroups)
        {
            var categoryId = categoryGroup.Key;
            var categoryName = allCategories.GetValueOrDefault(categoryId, "Unknown");
            var entries = categoryGroup.ToList();

            // Cluster descriptions within this category by token similarity
            var clusters = ClusterByTokenSimilarity(entries);

            foreach (var cluster in clusters.Where(c => c.Count >= MinClusterSize))
            {
                // Check if an existing rule already covers this cluster
                if (IsClusterCoveredByRules(cluster, existingRules))
                {
                    result.CoveredClusterCount++;
                    continue;
                }

                // Try deterministic: find a common token shared by all descriptions
                var commonToken = FindCommonToken(cluster);

                if (commonToken != null)
                {
                    // Validate the deterministic rule against the full history
                    if (ValidateRuleAgainstHistory(commonToken, categoryId, allHistory))
                    {
                        result.DeterministicSuggestions.Add(new PatternSuggestion
                        {
                            Pattern = commonToken,
                            SuggestedCategoryId = categoryId,
                            SuggestedCategoryName = categoryName,
                            ConfidenceScore = DeterministicConfidence,
                            MatchingTransactions = new List<Transaction>(), // No transactions in history-based analysis
                            DetectionMethod = "History Pattern Analysis",
                            Reasoning = $"All {cluster.Count} categorization history entries for '{categoryName}' share the token '{commonToken}'"
                        });
                    }
                }
                else
                {
                    // No common token — batch for AI analysis
                    result.AmbiguousClusters.Add(new AmbiguousCluster
                    {
                        CategoryId = categoryId,
                        CategoryName = categoryName,
                        Descriptions = cluster.Select(h => h.OriginalDescription).Distinct().ToList(),
                        NormalizedDescriptions = cluster.Select(h => h.NormalizedDescription).Distinct().ToList(),
                        TotalMatchCount = cluster.Sum(h => h.MatchCount)
                    });
                }
            }
        }

        _logger.LogInformation(
            "History analysis for user {UserId}: {Total} entries, {Deterministic} deterministic suggestions, " +
            "{Ambiguous} ambiguous clusters, {Covered} already covered",
            userId, result.TotalEntriesAnalyzed, result.DeterministicSuggestions.Count,
            result.AmbiguousClusters.Count, result.CoveredClusterCount);

        return result;
    }

    /// <summary>
    /// Clusters history entries within a single category by token overlap.
    /// Two entries are in the same cluster if they share at least one significant token.
    /// Uses union-find for efficient clustering.
    /// </summary>
    internal static List<List<CategorizationHistory>> ClusterByTokenSimilarity(
        List<CategorizationHistory> entries)
    {
        if (entries.Count == 0) return new List<List<CategorizationHistory>>();

        // Extract tokens for each entry
        var entryTokens = entries
            .Select(e => new
            {
                Entry = e,
                Tokens = new HashSet<string>(
                    DescriptionNormalizer.ExtractTokens(e.NormalizedDescription),
                    StringComparer.OrdinalIgnoreCase)
            })
            .ToList();

        // Union-Find
        var parent = Enumerable.Range(0, entries.Count).ToArray();
        int Find(int x) => parent[x] == x ? x : (parent[x] = Find(parent[x]));
        void Union(int a, int b)
        {
            var ra = Find(a); var rb = Find(b);
            if (ra != rb) parent[ra] = rb;
        }

        // Build inverted index: token → list of entry indices
        var tokenIndex = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < entryTokens.Count; i++)
        {
            foreach (var token in entryTokens[i].Tokens)
            {
                if (!tokenIndex.TryGetValue(token, out var list))
                {
                    list = new List<int>();
                    tokenIndex[token] = list;
                }
                list.Add(i);
            }
        }

        // Union entries that share any token
        foreach (var (_, indices) in tokenIndex)
        {
            for (int i = 1; i < indices.Count; i++)
            {
                Union(indices[0], indices[i]);
            }
        }

        // Group by root
        return Enumerable.Range(0, entries.Count)
            .GroupBy(Find)
            .Select(g => g.Select(i => entries[i]).ToList())
            .ToList();
    }

    /// <summary>
    /// Finds a common token (>= 4 chars) shared by all entries in the cluster.
    /// Returns the longest common token, or null if none exists.
    /// </summary>
    internal static string? FindCommonToken(List<CategorizationHistory> cluster)
    {
        if (cluster.Count == 0) return null;

        // Get tokens for each entry
        var tokenSets = cluster
            .Select(e => new HashSet<string>(
                DescriptionNormalizer.ExtractTokens(e.NormalizedDescription)
                    .Where(t => t.Length >= MinTokenLength),
                StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (tokenSets.Any(s => s.Count == 0)) return null;

        // Intersect all token sets
        var commonTokens = new HashSet<string>(tokenSets[0], StringComparer.OrdinalIgnoreCase);
        foreach (var tokenSet in tokenSets.Skip(1))
        {
            commonTokens.IntersectWith(tokenSet);
        }

        // Return the longest common token (most specific)
        return commonTokens
            .OrderByDescending(t => t.Length)
            .FirstOrDefault();
    }

    /// <summary>
    /// Checks if existing rules already cover most of the descriptions in a cluster.
    /// A cluster is "covered" if >= 80% of its entries match an existing rule for the same category.
    /// </summary>
    private static bool IsClusterCoveredByRules(
        List<CategorizationHistory> cluster,
        List<CategorizationRule> existingRules)
    {
        if (!existingRules.Any()) return false;

        int coveredCount = 0;
        foreach (var entry in cluster)
        {
            foreach (var rule in existingRules.Where(r => r.IsActive && r.CategoryId == entry.CategoryId))
            {
                if (DoesPatternMatch(rule, entry.OriginalDescription))
                {
                    coveredCount++;
                    break;
                }
            }
        }

        return (double)coveredCount / cluster.Count >= 0.80;
    }

    /// <summary>
    /// Validates a proposed "Contains" rule against the full history.
    /// Precision: of all history entries matching the pattern, what % maps to the target category? (>= 0.90)
    /// Recall: of all history entries in the target category, what % matches the pattern? (>= 0.60)
    /// </summary>
    internal bool ValidateRuleAgainstHistory(
        string pattern, int targetCategoryId, IReadOnlyList<CategorizationHistory> allHistory)
    {
        var matchingEntries = allHistory
            .Where(h => h.NormalizedDescription.Contains(pattern, StringComparison.OrdinalIgnoreCase)
                     || h.OriginalDescription.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var targetEntries = allHistory.Where(h => h.CategoryId == targetCategoryId).ToList();

        if (matchingEntries.Count == 0 || targetEntries.Count == 0)
            return false;

        // Precision: how many matching entries actually belong to the target category?
        var truePositives = matchingEntries.Count(h => h.CategoryId == targetCategoryId);
        var precision = (double)truePositives / matchingEntries.Count;

        // Recall: how many target-category entries does this pattern catch?
        var recall = (double)truePositives / targetEntries.Count;

        var isValid = precision >= MinPrecision && recall >= MinRecall;

        if (!isValid)
        {
            _logger.LogDebug(
                "Rule validation failed for pattern '{Pattern}' → category {CategoryId}: " +
                "precision={Precision:F2} (min {MinPrecision:F2}), recall={Recall:F2} (min {MinRecall:F2})",
                pattern, targetCategoryId, precision, MinPrecision, recall, MinRecall);
        }

        return isValid;
    }

    private static bool DoesPatternMatch(CategorizationRule rule, string description)
    {
        if (string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(rule.Pattern))
            return false;

        var comparison = rule.IsCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return rule.Type switch
        {
            RuleType.Contains => description.Contains(rule.Pattern, comparison),
            RuleType.StartsWith => description.StartsWith(rule.Pattern, comparison),
            RuleType.EndsWith => description.EndsWith(rule.Pattern, comparison),
            RuleType.Equals => description.Equals(rule.Pattern, comparison),
            RuleType.Regex => SafeRegexMatch(description, rule.Pattern, rule.IsCaseSensitive),
            _ => false
        };
    }

    private static bool SafeRegexMatch(string input, string pattern, bool caseSensitive)
    {
        try
        {
            var options = caseSensitive
                ? System.Text.RegularExpressions.RegexOptions.None
                : System.Text.RegularExpressions.RegexOptions.IgnoreCase;
            return System.Text.RegularExpressions.Regex.IsMatch(input, pattern, options,
                TimeSpan.FromMilliseconds(100));
        }
        catch
        {
            return false;
        }
    }
}
