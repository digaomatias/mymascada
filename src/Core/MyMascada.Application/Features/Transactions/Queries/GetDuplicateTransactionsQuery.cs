using MediatR;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Transactions.DTOs;
using MyMascada.Application.Features.Transactions.Mappings;

namespace MyMascada.Application.Features.Transactions.Queries;

public class GetDuplicateTransactionsQuery : IRequest<DuplicateTransactionsResponse>
{
    public Guid UserId { get; set; }
    public decimal AmountTolerance { get; set; } = 0.01m; // Allow small rounding differences in amount
    public int DateToleranceDays { get; set; } = 2; // Allow 2-day window for bank processing delays
    public bool IncludeReviewed { get; set; } = false; // Include already reviewed transactions
    public bool SameAccountOnly { get; set; } = false; // Only check within same account
    public decimal MinConfidence { get; set; } = 0.5m; // Minimum confidence score
}

public class GetDuplicateTransactionsQueryHandler : IRequestHandler<GetDuplicateTransactionsQuery, DuplicateTransactionsResponse>
{
    private static readonly System.Text.RegularExpressions.Regex AsteriskRegex = new(@"\*+", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex NonAlphanumericRegex = new(@"[^a-zA-Z0-9\s]", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex WhitespaceRegex = new(@"\s+", System.Text.RegularExpressions.RegexOptions.Compiled);

    private readonly ITransactionRepository _transactionRepository;
    private readonly IDuplicateExclusionRepository _duplicateExclusionRepository;

    public GetDuplicateTransactionsQueryHandler(
        ITransactionRepository transactionRepository,
        IDuplicateExclusionRepository duplicateExclusionRepository)
    {
        _transactionRepository = transactionRepository;
        _duplicateExclusionRepository = duplicateExclusionRepository;
    }

    public async Task<DuplicateTransactionsResponse> Handle(GetDuplicateTransactionsQuery request, CancellationToken cancellationToken)
    {
        // Get all transactions for the user (excluding transfers for now)
        var allTransactions = await _transactionRepository.GetAllForDuplicateDetectionAsync(request.UserId, request.IncludeReviewed);
        
        // Get all exclusions for this user to filter out previously dismissed duplicates
        var exclusions = await _duplicateExclusionRepository.GetByUserIdAsync(request.UserId);
        
        var duplicateGroups = new List<DuplicateGroupDto>();
        var processedTransactionIds = new HashSet<int>();
        
        // Group transactions by potential duplicates
        foreach (var transaction in allTransactions)
        {
            if (processedTransactionIds.Contains(transaction.Id))
                continue;
                
            var potentialDuplicates = FindPotentialDuplicates(transaction, allTransactions, request);
            
            if (potentialDuplicates.Any())
            {
                // Check if this group has been previously excluded by the user
                var groupTransactionIds = new List<int> { transaction.Id }
                    .Concat(potentialDuplicates.Select(d => d.Transaction.Id))
                    .ToList();
                
                // Skip this group if it matches any exclusion
                var isExcluded = exclusions.Any(exclusion => exclusion.AppliesToTransactions(groupTransactionIds));
                
                if (!isExcluded)
                {
                    var group = new DuplicateGroupDto
                    {
                        Id = Guid.NewGuid(),
                        Transactions = new List<TransactionDto> { TransactionMapper.ToDto(transaction) }
                            .Concat(potentialDuplicates.Select(d => TransactionMapper.ToDto(d.Transaction)))
                            .ToList(),
                        HighestConfidence = potentialDuplicates.Max(d => d.Confidence),
                        TotalAmount = Math.Abs(transaction.Amount),
                        DateRange = $"{transaction.TransactionDate:MMM dd} - {potentialDuplicates.Max(d => d.Transaction.TransactionDate):MMM dd}",
                        Description = transaction.Description
                    };
                    
                    duplicateGroups.Add(group);
                }
                
                // Mark all transactions in this group as processed (whether excluded or not)
                processedTransactionIds.Add(transaction.Id);
                foreach (var duplicate in potentialDuplicates)
                {
                    processedTransactionIds.Add(duplicate.Transaction.Id);
                }
            }
        }
        
        // Sort by highest confidence first
        duplicateGroups = duplicateGroups
            .OrderByDescending(g => g.HighestConfidence)
            .ThenByDescending(g => g.TotalAmount)
            .ToList();
        
        return new DuplicateTransactionsResponse
        {
            DuplicateGroups = duplicateGroups,
            TotalGroups = duplicateGroups.Count,
            TotalTransactions = duplicateGroups.Sum(g => g.Transactions.Count),
            ProcessedAt = DateTime.UtcNow
        };
    }
    
    private List<DuplicateMatch> FindPotentialDuplicates(
        Domain.Entities.Transaction sourceTransaction, 
        List<Domain.Entities.Transaction> allTransactions,
        GetDuplicateTransactionsQuery request)
    {
        var duplicates = new List<DuplicateMatch>();
        
        foreach (var candidate in allTransactions)
        {
            // Skip self
            if (candidate.Id == sourceTransaction.Id)
                continue;
                
            // Skip if different accounts when same account filter is enabled
            if (request.SameAccountOnly && candidate.AccountId != sourceTransaction.AccountId)
                continue;
                
            // Apply hard filters before calculating confidence
            
            // Hard filter: Date tolerance
            var daysDifference = Math.Abs((sourceTransaction.TransactionDate.Date - candidate.TransactionDate.Date).Days);
            if (daysDifference > request.DateToleranceDays)
                continue;
                
            // Hard filter: Amount tolerance — require near-exact match.
            // Different amounts are the strongest signal that transactions are NOT duplicates.
            var amountDifference = Math.Abs(sourceTransaction.Amount - candidate.Amount);
            if (amountDifference > request.AmountTolerance && amountDifference > Math.Abs(sourceTransaction.Amount) * 0.01m)
                continue; // Skip if amount difference exceeds both absolute tolerance and 1% relative tolerance
                
            var confidence = CalculateConfidenceScore(sourceTransaction, candidate, request);
            
            if (confidence >= request.MinConfidence)
            {
                duplicates.Add(new DuplicateMatch
                {
                    Transaction = candidate,
                    Confidence = confidence
                });
            }
        }
        
        return duplicates.OrderByDescending(d => d.Confidence).ToList();
    }
    
    private decimal CalculateConfidenceScore(
        Domain.Entities.Transaction source,
        Domain.Entities.Transaction candidate,
        GetDuplicateTransactionsQuery request)
    {
        decimal score = 0m;

        // Amount similarity (40% weight) — exact match is the strongest duplicate signal.
        // Candidates already pass the hard filter (within 1% or absolute tolerance).
        var amountDifference = Math.Abs(source.Amount - candidate.Amount);
        if (amountDifference <= request.AmountTolerance)
        {
            score += 0.4m; // Exact or near-exact match
        }
        else
        {
            score += 0.05m; // Within hard-filter range (≤1%) but not exact
        }

        // Date proximity (25% weight) — candidates already pass hard filter
        var daysDifference = Math.Abs((source.TransactionDate.Date - candidate.TransactionDate.Date).Days);
        if (daysDifference == 0)
        {
            score += 0.25m;
        }
        else if (daysDifference <= 1)
        {
            score += 0.2m; // 1-day difference (common for bank processing delays)
        }
        else if (daysDifference <= request.DateToleranceDays)
        {
            score += 0.15m;
        }

        // Description similarity (25% weight) — use both character-level and word-level matching
        var descriptionSimilarity = CalculateCombinedDescriptionSimilarity(
            source.Description?.Trim() ?? "",
            candidate.Description?.Trim() ?? "");
        if (descriptionSimilarity > 0.8m)
        {
            score += 0.25m;
        }
        else if (descriptionSimilarity > 0.6m)
        {
            score += 0.2m;
        }
        else if (descriptionSimilarity > 0.4m)
        {
            score += 0.15m;
        }
        else if (descriptionSimilarity > 0.2m)
        {
            score += 0.1m;
        }

        // Same account bonus (10% weight)
        if (source.AccountId == candidate.AccountId)
        {
            score += 0.1m;
        }

        // External ID penalty — only apply a small penalty when both have different external IDs.
        // Receipt-scanned or manual transactions won't have an external ID, so mixed pairs
        // (one with external ID, one without) should NOT be penalized.
        if (!string.IsNullOrEmpty(source.ExternalId) &&
            !string.IsNullOrEmpty(candidate.ExternalId) &&
            source.ExternalId != candidate.ExternalId)
        {
            score -= 0.1m;
        }

        return Math.Max(0m, Math.Min(1m, score));
    }
    
    /// <summary>
    /// Calculates combined description similarity using both character-level (Levenshtein)
    /// and word-level (Jaccard) matching. Takes the higher score.
    /// Bank descriptions are often truncated differently for the same merchant,
    /// so word overlap catches cases that Levenshtein misses.
    /// </summary>
    private static decimal CalculateCombinedDescriptionSimilarity(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) && string.IsNullOrEmpty(str2))
            return 1m;

        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
            return 0m;

        // Clean merchant descriptions: remove card masks, special chars
        var clean1 = CleanMerchantDescription(str1);
        var clean2 = CleanMerchantDescription(str2);

        var levenshteinScore = CalculateStringSimilarity(clean1, clean2);
        var wordScore = CalculateWordSimilarity(clean1, clean2);

        return Math.Max(levenshteinScore, wordScore);
    }

    /// <summary>
    /// Cleans merchant description by removing card number masks (****),
    /// non-alphanumeric characters, and normalizing whitespace.
    /// </summary>
    private static string CleanMerchantDescription(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // Remove card number masks and asterisks
        var cleaned = AsteriskRegex.Replace(input, " ");
        // Remove non-alphanumeric (keep letters, digits, spaces)
        cleaned = NonAlphanumericRegex.Replace(cleaned, " ");
        // Normalize whitespace
        cleaned = WhitespaceRegex.Replace(cleaned.ToLowerInvariant().Trim(), " ");
        return cleaned;
    }

    /// <summary>
    /// Word-level Jaccard similarity with prefix matching.
    /// "new world ston" vs "new world stonefields" matches well because
    /// "ston" is a prefix of "stonefields".
    /// </summary>
    private static decimal CalculateWordSimilarity(string str1, string str2)
    {
        var words1 = str1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words2 = str2.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (words1.Length == 0 && words2.Length == 0) return 1m;
        if (words1.Length == 0 || words2.Length == 0) return 0m;

        // Count matches including prefix matching (min 3 chars)
        int matches = 0;
        var used = new bool[words2.Length];

        foreach (var w1 in words1)
        {
            for (int j = 0; j < words2.Length; j++)
            {
                if (used[j]) continue;

                if (string.Equals(w1, words2[j], StringComparison.OrdinalIgnoreCase) ||
                    (w1.Length >= 3 && words2[j].StartsWith(w1, StringComparison.OrdinalIgnoreCase)) ||
                    (words2[j].Length >= 3 && w1.StartsWith(words2[j], StringComparison.OrdinalIgnoreCase)))
                {
                    matches++;
                    used[j] = true;
                    break;
                }
            }
        }

        // Similarity = matches / max word count (similar to Jaccard)
        var maxWords = Math.Max(words1.Length, words2.Length);
        return (decimal)matches / maxWords;
    }

    private static decimal CalculateStringSimilarity(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) && string.IsNullOrEmpty(str2))
            return 1m;

        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
            return 0m;

        var normalized1 = NormalizeString(str1);
        var normalized2 = NormalizeString(str2);

        var distance = LevenshteinDistance(normalized1, normalized2);
        var maxLength = Math.Max(normalized1.Length, normalized2.Length);

        return 1m - (decimal)distance / maxLength;
    }

    private static string NormalizeString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        return WhitespaceRegex.Replace(input.ToLowerInvariant().Trim(), " ");
    }
    
    private static int LevenshteinDistance(string s1, string s2)
    {
        if (s1.Length == 0) return s2.Length;
        if (s2.Length == 0) return s1.Length;
        
        var matrix = new int[s1.Length + 1, s2.Length + 1];
        
        for (int i = 0; i <= s1.Length; i++)
            matrix[i, 0] = i;
            
        for (int j = 0; j <= s2.Length; j++)
            matrix[0, j] = j;
            
        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
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

internal class DuplicateMatch
{
    public Domain.Entities.Transaction Transaction { get; set; } = null!;
    public decimal Confidence { get; set; }
}