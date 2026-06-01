using MyMascada.Application.Common.Interfaces;
using MyMascada.Domain.Entities;
using System.Text.RegularExpressions;

namespace MyMascada.Application.Features.RuleSuggestions.Services;

/// <summary>
/// Merchant-centric rule suggestion analyzer.
///
/// Instead of scoring every word that survives a stop-word filter, this analyzer mines candidate
/// merchant phrases from transaction descriptions and ranks them by how well they *discriminate* a
/// category across the whole dataset — not just by how pure they look over a handful of already
/// labelled samples. Structural noise (the account holder's own name, card product names, bank
/// prefixes, reference/card numbers) is stripped before mining, so rules no longer key off the
/// cardholder name printed on every statement line. Overlapping candidates are de-duplicated by
/// their full-dataset match set, so the same merchant can only be suggested once.
/// </summary>
public class BasicRuleSuggestionAnalyzer : IRuleSuggestionAnalyzer
{
    private readonly ICategoryRepository _categoryRepository;

    public const string AnalysisMethodName = "Merchant Pattern Analysis";
    public string AnalysisMethod => AnalysisMethodName;
    public bool RequiresAI => false;

    // --- Tuning knobs -------------------------------------------------------
    // Minimum transactions a pattern must match (anywhere in the dataset) to be considered.
    private const int MinSupport = 3;
    // Minimum number of *categorized* matches required as evidence for a category.
    private const int MinCategorizedEvidence = 3;
    // Fraction of a pattern's categorized matches that must share the dominant category.
    private const double MinPurity = 0.8;
    // A pattern matching more than this fraction of ALL transactions is treated as structural
    // noise (e.g. a bank prefix or an un-stripped name) and discarded.
    private const double MaxDatasetFraction = 0.4;
    // Two suggestions are considered redundant when their match sets overlap by at least this much.
    private const double OverlapRedundancyThreshold = 0.7;
    private const int MinMeaningfulTokenLength = 3;
    private const int MaxPhraseTokens = 3;

    // Structural noise: card products, bank prefixes, and statement boilerplate that carry no
    // merchant identity. Merged with the holder-name tokens supplied per-request.
    private static readonly HashSet<string> StructuralNoise = new(StringComparer.OrdinalIgnoreCase)
    {
        // generic English / boilerplate
        "THE", "AND", "FOR", "WITH", "FROM", "PURCHASE", "PAYMENT", "PAYMENTS", "TRANSACTION",
        "DEBIT", "CREDIT", "CARD", "ACCOUNT", "DATE", "TIME", "LOCATION", "STORE", "SHOP",
        "INC", "LLC", "LTD", "LIMITED", "PTY", "CORP", "REF", "AUTH", "APPROVED", "ONLINE",
        "WWW", "COM", "RECURRING", "DIRECT", "AUTOMATIC", "VALUE",
        // card products / schemes (NZ + general)
        "VISA", "MASTERCARD", "MCARD", "AMEX", "EFTPOS", "GEM", "PAYWAVE", "PAYPASS", "POS",
        // banks
        "ANZ", "ASB", "BNZ", "WESTPAC", "KIWIBANK"
    };

    // Common dictionary words that make weak single-token patterns (high false-positive risk on
    // their own). Only used as a scoring penalty — never a hard ban, since a word like "SAVE" can
    // still be the only token that links spelling variants of a merchant (e.g. PAK'nSAVE).
    private static readonly HashSet<string> WeakSingleTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "SAVE", "SHOP", "STORE", "PLUS", "MART", "CLUB", "GROUP", "CENTRAL", "EXPRESS", "PRIME"
    };

    public BasicRuleSuggestionAnalyzer(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public Task<List<PatternSuggestion>> AnalyzePatternsAsync(RuleAnalysisInput input, CancellationToken cancellationToken = default)
    {
        var transactions = input.Transactions
            .Where(t => !string.IsNullOrWhiteSpace(t.Description))
            .ToList();

        if (transactions.Count == 0)
            return Task.FromResult(new List<PatternSuggestion>());

        var categoriesById = input.AvailableCategories
            .GroupBy(c => c.Id)
            .ToDictionary(g => g.Key, g => g.First());

        // Noise vocabulary = structural terms + the holder's own name tokens.
        var noise = new HashSet<string>(StructuralNoise, StringComparer.OrdinalIgnoreCase);
        foreach (var token in input.AccountHolderNameTokens)
        {
            if (!string.IsNullOrWhiteSpace(token))
                noise.Add(token.Trim());
        }

        // 1. Mine candidate merchant phrases (deduplicated across the dataset).
        var candidates = MineCandidatePhrases(transactions, noise);

        // 2. Score each candidate by discriminative power over the WHOLE dataset, then rank:
        //    coverage first (prefer one broad rule per merchant over several narrow ones), then
        //    confidence, then prefer a multi-token phrase over a single word covering the same
        //    transactions (so "FLIGHT CENTRE" wins over "FLIGHT", and the merchant token wins over
        //    an incidental location token).
        var ranked = candidates
            .Select(c => ScoreCandidate(c, transactions, categoriesById, input.MinConfidenceThreshold))
            .Where(r => r != null)
            .Select(r => r!.Value)
            .OrderByDescending(r => r.suggestion.MatchingTransactions.Count)
            .ThenByDescending(r => r.suggestion.ConfidenceScore)
            .ThenByDescending(r => r.tokenCount)
            .ThenByDescending(r => r.suggestion.Pattern.Length)
            .ToList();

        // 3. Greedy de-overlap: a merchant's transactions can only back one suggestion.
        var result = DeduplicateByMatchSet(ranked, input.MaxSuggestions);

        return Task.FromResult(result);
    }

    // Splits on any run of non-alphanumeric characters. Compiled once and reused per transaction.
    private static readonly Regex TokenSplitRegex = new(@"[^A-Z0-9]+", RegexOptions.Compiled);

    /// <summary>
    /// Builds the set of candidate phrases (1..N consecutive meaningful tokens) seen across all
    /// transactions. Phrases are mined within contiguous meaningful segments only, so a phrase can
    /// never bridge over a stripped noise/name/number token — which would form a string that never
    /// occurred contiguously in the source.
    /// </summary>
    private static HashSet<string> MineCandidatePhrases(List<Transaction> transactions, HashSet<string> noise)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var transaction in transactions)
        {
            foreach (var segment in MeaningfulTokenSegments(transaction.Description, noise))
            {
                for (int start = 0; start < segment.Count; start++)
                {
                    for (int len = 1; len <= MaxPhraseTokens && start + len <= segment.Count; len++)
                    {
                        candidates.Add(string.Join(' ', segment.GetRange(start, len)));
                    }
                }
            }
        }

        return candidates;
    }

    /// <summary>
    /// Splits a description into contiguous segments of meaningful tokens. A structural/name noise
    /// word, a too-short token, or a mostly-digit token (reference and card numbers) acts as a
    /// separator that ends the current segment — so tokens on either side of stripped noise are
    /// never treated as adjacent.
    /// </summary>
    private static List<List<string>> MeaningfulTokenSegments(string description, HashSet<string> noise)
    {
        var segments = new List<List<string>>();
        var current = new List<string>();

        foreach (var token in TokenSplitRegex.Split(description.ToUpperInvariant()))
        {
            var isSeparator = token.Length < MinMeaningfulTokenLength
                              || IsMostlyDigits(token)
                              || noise.Contains(token);

            if (isSeparator)
            {
                if (current.Count > 0)
                {
                    segments.Add(current);
                    current = new List<string>();
                }
            }
            else
            {
                current.Add(token);
            }
        }

        if (current.Count > 0)
            segments.Add(current);

        return segments;
    }

    private static bool IsMostlyDigits(string token)
    {
        var digitCount = 0;
        for (var i = 0; i < token.Length; i++)
        {
            if (char.IsDigit(token[i]))
                digitCount++;
        }
        return digitCount > 0 && digitCount * 2 >= token.Length;
    }

    /// <summary>
    /// Scores one candidate phrase against the full dataset using the SAME matching semantics as a
    /// real rule (case-insensitive Contains). Returns null when the candidate fails any gate.
    /// </summary>
    private static (PatternSuggestion suggestion, int tokenCount)? ScoreCandidate(
        string pattern,
        List<Transaction> allTransactions,
        Dictionary<int, Category> categoriesById,
        double minConfidence)
    {
        var matched = allTransactions
            .Where(t => t.Description != null &&
                        t.Description.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matched.Count < MinSupport)
            return null;

        // Structural-noise guard: a pattern matching a huge share of all spending is not a merchant.
        var datasetFraction = (double)matched.Count / allTransactions.Count;
        if (datasetFraction > MaxDatasetFraction)
            return null;

        var categorized = matched
            .Where(t => t.CategoryId.HasValue && categoriesById.ContainsKey(t.CategoryId!.Value))
            .ToList();

        if (categorized.Count < MinCategorizedEvidence)
            return null;

        var categoryGroups = categorized
            .GroupBy(t => t.CategoryId!.Value)
            .OrderByDescending(g => g.Count())
            .ToList();

        var dominant = categoryGroups[0];
        var purity = (double)dominant.Count() / categorized.Count;
        if (purity < MinPurity)
            return null;

        var breadth = categoryGroups.Count; // distinct categories the pattern leaks into
        var category = categoriesById[dominant.Key];
        var tokenCount = pattern.Count(c => c == ' ') + 1;

        // Confidence = purity, penalized for cross-category leakage and weak single tokens.
        var breadthPenalty = breadth == 1 ? 1.0 : Math.Pow(0.8, breadth - 1);
        var weakTokenPenalty = (tokenCount == 1 && WeakSingleTokens.Contains(pattern)) ? 0.8 : 1.0;
        var confidence = purity * breadthPenalty * weakTokenPenalty;

        if (confidence < minConfidence)
            return null;

        var purityPercent = (int)Math.Round(purity * 100);
        var suggestion = new PatternSuggestion
        {
            Pattern = pattern,
            SuggestedCategoryId = category.Id,
            SuggestedCategoryName = category.Name,
            ConfidenceScore = Math.Clamp(confidence, 0, 1),
            MatchingTransactions = matched,
            DetectionMethod = AnalysisMethodName,
            Reasoning = $"'{pattern}' matches {matched.Count} transactions, {purityPercent}% categorized as {category.Name}"
        };

        return (suggestion, tokenCount);
    }

    /// <summary>
    /// Greedily accepts the highest-ranked suggestions, dropping any whose matched transactions are
    /// already largely covered by an accepted suggestion. This collapses spelling variants of the
    /// same merchant (whose match sets nest under a shared token) into a single suggestion and
    /// avoids two rules competing for the same transactions.
    /// </summary>
    private static List<PatternSuggestion> DeduplicateByMatchSet(
        List<(PatternSuggestion suggestion, int tokenCount)> ranked,
        int maxSuggestions)
    {
        var accepted = new List<(PatternSuggestion suggestion, HashSet<int> ids)>();

        foreach (var (suggestion, _) in ranked)
        {
            var ids = suggestion.MatchingTransactions.Select(t => t.Id).ToHashSet();
            if (ids.Count == 0)
                continue;

            var redundant = accepted.Any(a =>
            {
                var overlap = ids.Count(id => a.ids.Contains(id));
                return (double)overlap / ids.Count >= OverlapRedundancyThreshold;
            });

            if (redundant)
                continue;

            accepted.Add((suggestion, ids));
            if (accepted.Count >= maxSuggestions)
                break;
        }

        return accepted.Select(a => a.suggestion).ToList();
    }
}
