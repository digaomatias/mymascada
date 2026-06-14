using System.Text.RegularExpressions;

namespace MyMascada.Domain.Common;

/// <summary>
/// Shared helpers for normalizing transaction descriptions into stable merchant keys,
/// measuring similarity between keys, and grouping mutually-similar keys together.
///
/// Centralizing this logic prevents drift between the pattern-detection sites
/// (on-demand <c>RecurringPatternService</c>, persisted <c>RecurringPatternPersistenceService</c>,
/// and the <c>RecurringPattern</c> entity's transaction matching).
/// </summary>
public static class MerchantNormalizer
{
    // Minimum length of an embedded numeric token to be treated as a reference number.
    // Kept at 3 so short merchant numbers (e.g. "3 Mobile") survive while bank
    // reference runs (e.g. "3301234") are stripped.
    private const int MinNumericTokenLength = 3;

    // A token is treated as an alphanumeric reference (and stripped) when it is at least
    // this long AND at least this fraction of its characters are digits.
    private const int MinAlphanumericRefLength = 5;
    private const double AlphanumericDigitRatio = 0.4;

    /// <summary>
    /// Default similarity threshold used to decide whether two normalized merchant keys
    /// represent the same merchant (0.0 - 1.0).
    /// </summary>
    public const decimal DefaultSimilarityThreshold = 0.8m;

    /// <summary>
    /// Normalizes a raw transaction description into a stable merchant key
    /// (lowercase, prefixes/dates/reference-numbers removed, whitespace collapsed).
    /// </summary>
    public static string Normalize(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return string.Empty;

        // Convert to lowercase and collapse whitespace.
        var normalized = Regex.Replace(description.ToLowerInvariant().Trim(), @"\s+", " ");

        // Remove common transaction prefixes.
        normalized = Regex.Replace(normalized, @"^(purchase\s+|payment\s+|pos\s+|debit\s+|eftpos\s+)", "");

        // Remove explicit reference numbers (patterns like #123, REF:ABC123, ID:987654).
        normalized = Regex.Replace(normalized, @"(#|ref:?|id:?)\s*[\w\d-]+", "");

        // Remove dates (patterns like 01/15, 12-25-2024).
        normalized = Regex.Replace(normalized, @"\d{1,2}[/-]\d{1,2}([/-]\d{2,4})?", "");

        // Remove time patterns (e.g. 14:30, 2:05pm).
        normalized = Regex.Replace(normalized, @"\d{1,2}:\d{2}(:\d{2})?(\s*(am|pm))?", "");

        // Strip embedded/standalone reference tokens that NZ (and other) bank feeds append
        // to an otherwise-stable merchant string (e.g. "ami insurance amiinsurance 3301234").
        // Tokenize on whitespace and drop any token that looks like a reference number.
        var tokens = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => !IsReferenceToken(token))
            .ToList();

        // Collapse a doubled merchant-word artifact like "ami insurance amiinsurance",
        // where one token is the concatenation of two adjacent preceding tokens.
        tokens = CollapseConcatenatedDuplicate(tokens);

        normalized = string.Join(" ", tokens);

        // Remove any trailing bare numbers left over.
        normalized = Regex.Replace(normalized, @"\s+\d+$", "");

        // Final whitespace cleanup.
        return Regex.Replace(normalized.Trim(), @"\s+", " ");
    }

    /// <summary>
    /// Determines whether a token should be treated as a reference number and stripped.
    /// </summary>
    private static bool IsReferenceToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return false;

        // Pure numeric run of meaningful length (e.g. "3301234").
        if (token.Length >= MinNumericTokenLength && token.All(char.IsDigit))
            return true;

        // Long alphanumeric token that is mostly digits (e.g. "inv00123ab", "x9f8273").
        if (token.Length >= MinAlphanumericRefLength)
        {
            var digitCount = token.Count(char.IsDigit);
            if (digitCount > 0 && (double)digitCount / token.Length >= AlphanumericDigitRatio)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Collapses a doubled merchant-word artifact where a token equals the concatenation
    /// of the immediately preceding tokens (e.g. ["ami", "insurance", "amiinsurance"] -&gt;
    /// ["ami", "insurance"]). Conservative: only removes a token that is an exact
    /// concatenation of preceding adjacent tokens.
    /// </summary>
    private static List<string> CollapseConcatenatedDuplicate(List<string> tokens)
    {
        if (tokens.Count < 2)
            return tokens;

        var result = new List<string>();
        foreach (var token in tokens)
        {
            var isConcatenationOfPrevious = false;

            // Try to match this token against the concatenation of the trailing 2..N
            // tokens already accepted.
            for (var take = 2; take <= result.Count; take++)
            {
                var candidate = string.Concat(result.Skip(result.Count - take));
                if (string.Equals(candidate, token, StringComparison.Ordinal))
                {
                    isConcatenationOfPrevious = true;
                    break;
                }
            }

            if (!isConcatenationOfPrevious)
                result.Add(token);
        }

        return result;
    }

    /// <summary>
    /// Calculates normalized Levenshtein similarity (0.0 - 1.0) between two keys.
    /// </summary>
    public static decimal CalculateSimilarity(string? str1, string? str2)
    {
        if (string.IsNullOrEmpty(str1) && string.IsNullOrEmpty(str2))
            return 1m;

        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
            return 0m;

        var distance = LevenshteinDistance(str1, str2);
        var maxLength = Math.Max(str1.Length, str2.Length);

        return 1m - (decimal)distance / maxLength;
    }

    /// <summary>
    /// Groups keys into connected components where any two keys with similarity above
    /// <paramref name="threshold"/> are considered linked. Uses union-find so the grouping
    /// is transitive: if A~B and B~C but A is not directly similar to C, all three still
    /// collapse into one group.
    /// </summary>
    /// <returns>
    /// A mapping from each input key to its canonical group representative. Callers can
    /// group by the representative to merge mutually-similar keys.
    /// </returns>
    public static Dictionary<string, string> GroupSimilarKeys(
        IReadOnlyCollection<string> keys,
        decimal? threshold = null)
    {
        var effectiveThreshold = threshold ?? DefaultSimilarityThreshold;
        var keyList = keys.Distinct().ToList();

        // Union-find parent pointers keyed by index into keyList.
        var parent = Enumerable.Range(0, keyList.Count).ToArray();

        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]]; // path compression
                x = parent[x];
            }
            return x;
        }

        void Union(int a, int b)
        {
            var rootA = Find(a);
            var rootB = Find(b);
            if (rootA != rootB)
                parent[rootB] = rootA;
        }

        for (var i = 0; i < keyList.Count; i++)
        {
            for (var j = i + 1; j < keyList.Count; j++)
            {
                if (CalculateSimilarity(keyList[i], keyList[j]) > effectiveThreshold)
                    Union(i, j);
            }
        }

        // Map each key to the (deterministic) canonical key of its component.
        var componentCanonical = new Dictionary<int, string>();
        for (var i = 0; i < keyList.Count; i++)
        {
            var root = Find(i);
            if (!componentCanonical.TryGetValue(root, out var current)
                || string.CompareOrdinal(keyList[i], current) < 0)
            {
                componentCanonical[root] = keyList[i];
            }
        }

        var mapping = new Dictionary<string, string>();
        for (var i = 0; i < keyList.Count; i++)
        {
            mapping[keyList[i]] = componentCanonical[Find(i)];
        }

        return mapping;
    }

    private static int LevenshteinDistance(string s1, string s2)
    {
        if (s1.Length == 0) return s2.Length;
        if (s2.Length == 0) return s1.Length;

        var matrix = new int[s1.Length + 1, s2.Length + 1];

        for (var i = 0; i <= s1.Length; i++)
            matrix[i, 0] = i;

        for (var j = 0; j <= s2.Length; j++)
            matrix[0, j] = j;

        for (var i = 1; i <= s1.Length; i++)
        {
            for (var j = 1; j <= s2.Length; j++)
            {
                var cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[s1.Length, s2.Length];
    }
}
