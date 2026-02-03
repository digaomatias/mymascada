using System.Text.RegularExpressions;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.UpcomingBills.DTOs;
using MyMascada.Domain.Entities;

namespace MyMascada.Application.Features.UpcomingBills.Services;

/// <summary>
/// Service for detecting recurring payment patterns from transaction history.
/// Queries from persisted patterns first, with fallback to on-demand calculation.
/// </summary>
public class RecurringPatternService : IRecurringPatternService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IRecurringPatternRepository? _patternRepository;

    // Pattern detection parameters
    private const int LookbackMonths = 6;
    private const int MinOccurrencesForPattern = 2;
    private const decimal MinConfidenceThreshold = 0.5m;

    // Interval detection ranges (in days)
    private const int WeeklyMinDays = 5;
    private const int WeeklyMaxDays = 9;
    private const int BiweeklyMinDays = 12;
    private const int BiweeklyMaxDays = 16;
    private const int MonthlyMinDays = 26;
    private const int MonthlyMaxDays = 35;

    public RecurringPatternService(
        ITransactionRepository transactionRepository,
        IRecurringPatternRepository? patternRepository = null)
    {
        _transactionRepository = transactionRepository;
        _patternRepository = patternRepository;
    }

    public async Task<UpcomingBillsResponse> GetUpcomingBillsAsync(
        Guid userId,
        int daysAhead = 7,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var endDate = today.AddDays(daysAhead);

        // Try to use persisted patterns first (if repository is available)
        if (_patternRepository != null)
        {
            var persistedBills = await GetUpcomingBillsFromPersistedDataAsync(
                userId, today, endDate, cancellationToken);

            if (persistedBills.Bills.Any())
            {
                return persistedBills;
            }
        }

        // Fallback to on-demand calculation from transaction history
        return await CalculateUpcomingBillsOnDemandAsync(userId, today, daysAhead);
    }

    /// <summary>
    /// Gets upcoming bills from persisted recurring patterns
    /// </summary>
    private async Task<UpcomingBillsResponse> GetUpcomingBillsFromPersistedDataAsync(
        Guid userId,
        DateTime today,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var upcomingPatterns = await _patternRepository!.GetUpcomingAsync(
            userId, today, endDate, cancellationToken);

        if (!upcomingPatterns.Any())
        {
            return new UpcomingBillsResponse
            {
                Bills = new List<UpcomingBillDto>(),
                TotalBillsCount = 0,
                TotalExpectedAmount = 0
            };
        }

        var bills = upcomingPatterns.Select(p => new UpcomingBillDto
        {
            PatternId = p.Id,
            MerchantName = p.MerchantName,
            ExpectedAmount = Math.Round(p.AverageAmount, 2),
            ExpectedDate = p.NextExpectedDate,
            DaysUntilDue = p.GetDaysUntilDue(today),
            ConfidenceScore = Math.Round(p.Confidence, 2),
            ConfidenceLevel = p.GetConfidenceLevel(),
            Interval = p.GetIntervalName(),
            OccurrenceCount = p.OccurrenceCount
        })
        .OrderBy(b => b.DaysUntilDue)
        .ThenByDescending(b => b.ConfidenceScore)
        .ToList();

        return new UpcomingBillsResponse
        {
            Bills = bills,
            TotalBillsCount = bills.Count,
            TotalExpectedAmount = bills.Sum(b => b.ExpectedAmount)
        };
    }

    /// <summary>
    /// Calculates upcoming bills on-demand from transaction history (fallback method)
    /// </summary>
    private async Task<UpcomingBillsResponse> CalculateUpcomingBillsOnDemandAsync(
        Guid userId,
        DateTime today,
        int daysAhead)
    {
        var startDate = today.AddMonths(-LookbackMonths);

        // Get expenses from last 6 months (negative amounts = expenses)
        var transactions = await _transactionRepository.GetByDateRangeAsync(userId, startDate, today);
        var expenses = transactions
            .Where(t => t.Amount < 0 && !t.IsDeleted && t.TransferId == null)
            .ToList();

        if (!expenses.Any())
        {
            return new UpcomingBillsResponse
            {
                Bills = new List<UpcomingBillDto>(),
                TotalBillsCount = 0,
                TotalExpectedAmount = 0
            };
        }

        // Group by normalized description
        var groupedTransactions = GroupByNormalizedDescription(expenses);

        // Detect recurring patterns
        var upcomingBills = new List<UpcomingBillDto>();

        foreach (var group in groupedTransactions)
        {
            if (group.Value.Count < MinOccurrencesForPattern)
                continue;

            var pattern = DetectPattern(group.Key, group.Value, today, daysAhead);
            if (pattern != null && pattern.ConfidenceScore >= MinConfidenceThreshold)
            {
                upcomingBills.Add(pattern);
            }
        }

        // Sort by days until due (soonest first)
        upcomingBills = upcomingBills
            .OrderBy(b => b.DaysUntilDue)
            .ThenByDescending(b => b.ConfidenceScore)
            .ToList();

        return new UpcomingBillsResponse
        {
            Bills = upcomingBills,
            TotalBillsCount = upcomingBills.Count,
            TotalExpectedAmount = upcomingBills.Sum(b => Math.Abs(b.ExpectedAmount))
        };
    }

    private Dictionary<string, List<Transaction>> GroupByNormalizedDescription(List<Transaction> transactions)
    {
        var groups = new Dictionary<string, List<Transaction>>();

        foreach (var transaction in transactions)
        {
            var normalizedKey = NormalizeDescription(transaction.Description);
            if (string.IsNullOrWhiteSpace(normalizedKey))
                continue;

            if (!groups.ContainsKey(normalizedKey))
            {
                groups[normalizedKey] = new List<Transaction>();
            }
            groups[normalizedKey].Add(transaction);
        }

        // Merge similar descriptions using Levenshtein distance
        return MergeSimilarGroups(groups);
    }

    private Dictionary<string, List<Transaction>> MergeSimilarGroups(Dictionary<string, List<Transaction>> groups)
    {
        var mergedGroups = new Dictionary<string, List<Transaction>>();
        var processedKeys = new HashSet<string>();

        foreach (var group in groups.OrderByDescending(g => g.Value.Count))
        {
            if (processedKeys.Contains(group.Key))
                continue;

            var mergedList = new List<Transaction>(group.Value);
            processedKeys.Add(group.Key);

            // Find similar groups
            foreach (var otherGroup in groups)
            {
                if (processedKeys.Contains(otherGroup.Key))
                    continue;

                var similarity = CalculateStringSimilarity(group.Key, otherGroup.Key);
                if (similarity > 0.8m) // 80% similar = same merchant
                {
                    mergedList.AddRange(otherGroup.Value);
                    processedKeys.Add(otherGroup.Key);
                }
            }

            mergedGroups[group.Key] = mergedList;
        }

        return mergedGroups;
    }

    private UpcomingBillDto? DetectPattern(string merchantName, List<Transaction> transactions, DateTime today, int daysAhead)
    {
        // Sort transactions by date (oldest first)
        var sortedTransactions = transactions.OrderBy(t => t.TransactionDate).ToList();

        if (sortedTransactions.Count < MinOccurrencesForPattern)
            return null;

        // Calculate intervals between consecutive transactions
        var intervals = new List<int>();
        for (int i = 1; i < sortedTransactions.Count; i++)
        {
            var daysBetween = (sortedTransactions[i].TransactionDate.Date - sortedTransactions[i - 1].TransactionDate.Date).Days;
            if (daysBetween > 0)
            {
                intervals.Add(daysBetween);
            }
        }

        if (!intervals.Any())
            return null;

        var averageInterval = intervals.Average();
        var detectedInterval = DetectRecurrenceInterval(averageInterval);

        if (detectedInterval == null)
            return null;

        // Calculate next expected date
        var lastTransaction = sortedTransactions.Last();
        var expectedDate = lastTransaction.TransactionDate.Date.AddDays((int)detectedInterval.Value);

        // Check if the expected date falls within our window
        var daysUntilDue = (expectedDate - today).Days;
        if (daysUntilDue < 0 || daysUntilDue > daysAhead)
            return null;

        // Calculate expected amount (average of all amounts, made positive)
        var amounts = sortedTransactions.Select(t => Math.Abs(t.Amount)).ToList();
        var expectedAmount = amounts.Average();

        // Calculate confidence score
        var confidence = CalculateConfidence(intervals, amounts, (int)detectedInterval.Value);

        if (confidence < MinConfidenceThreshold)
            return null;

        // Format merchant name (use most recent transaction's description)
        var displayName = FormatMerchantName(sortedTransactions.Last().Description);

        return new UpcomingBillDto
        {
            MerchantName = displayName,
            ExpectedAmount = Math.Round(expectedAmount, 2),
            ExpectedDate = expectedDate,
            DaysUntilDue = daysUntilDue,
            ConfidenceScore = Math.Round(confidence, 2),
            ConfidenceLevel = confidence >= 0.75m ? "High" : "Medium",
            Interval = GetIntervalName(detectedInterval.Value),
            OccurrenceCount = sortedTransactions.Count
        };
    }

    private RecurrenceInterval? DetectRecurrenceInterval(double averageInterval)
    {
        if (averageInterval >= WeeklyMinDays && averageInterval <= WeeklyMaxDays)
            return RecurrenceInterval.Weekly;
        if (averageInterval >= BiweeklyMinDays && averageInterval <= BiweeklyMaxDays)
            return RecurrenceInterval.Biweekly;
        if (averageInterval >= MonthlyMinDays && averageInterval <= MonthlyMaxDays)
            return RecurrenceInterval.Monthly;

        return null;
    }

    private string GetIntervalName(RecurrenceInterval interval)
    {
        return interval switch
        {
            RecurrenceInterval.Weekly => "Weekly",
            RecurrenceInterval.Biweekly => "Biweekly",
            RecurrenceInterval.Monthly => "Monthly",
            _ => "Unknown"
        };
    }

    private decimal CalculateConfidence(List<int> intervals, List<decimal> amounts, int expectedInterval)
    {
        // Weights: occurrences (40%) + interval consistency (35%) + amount consistency (25%)
        decimal confidenceScore = 0m;

        // 1. Occurrence count (40% weight)
        // More occurrences = higher confidence
        var occurrenceWeight = 0.40m;
        var occurrenceScore = intervals.Count switch
        {
            >= 5 => 1.0m,
            4 => 0.9m,
            3 => 0.75m,
            2 => 0.5m,
            _ => 0m
        };
        confidenceScore += occurrenceWeight * occurrenceScore;

        // 2. Interval consistency (35% weight)
        // How consistent are the intervals compared to expected?
        var intervalWeight = 0.35m;
        if (intervals.Any())
        {
            var intervalDeviation = intervals.Select(i => Math.Abs(i - expectedInterval)).Average();
            var maxDeviation = expectedInterval * 0.3; // Allow 30% deviation
            var intervalScore = Math.Max(0, 1 - (decimal)(intervalDeviation / maxDeviation));
            confidenceScore += intervalWeight * intervalScore;
        }

        // 3. Amount consistency (25% weight)
        // How consistent are the amounts?
        var amountWeight = 0.25m;
        if (amounts.Count > 1)
        {
            var avgAmount = amounts.Average();
            if (avgAmount > 0)
            {
                var amountDeviation = amounts.Select(a => Math.Abs(a - avgAmount)).Average();
                var maxAmountDeviation = avgAmount * 0.1m; // Allow 10% deviation
                var amountScore = Math.Max(0, 1 - amountDeviation / maxAmountDeviation);
                confidenceScore += amountWeight * amountScore;
            }
        }
        else
        {
            // Single amount, assume consistent
            confidenceScore += amountWeight * 0.5m;
        }

        return Math.Min(1.0m, confidenceScore);
    }

    private string NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return string.Empty;

        // Convert to lowercase and remove extra whitespace
        var normalized = Regex.Replace(description.ToLowerInvariant().Trim(), @"\s+", " ");

        // Remove common transaction prefixes/suffixes
        normalized = Regex.Replace(normalized, @"^(purchase\s+|payment\s+|pos\s+|debit\s+|eftpos\s+)", "");

        // Remove reference numbers (patterns like #123, REF:ABC123, etc.)
        normalized = Regex.Replace(normalized, @"(#|ref:?|id:?)\s*[\w\d-]+", "");

        // Remove dates (patterns like 01/15, 15-Jan, etc.)
        normalized = Regex.Replace(normalized, @"\d{1,2}[/-]\d{1,2}([/-]\d{2,4})?", "");

        // Remove time patterns
        normalized = Regex.Replace(normalized, @"\d{1,2}:\d{2}(:\d{2})?(\s*(am|pm))?", "");

        // Remove trailing numbers that might be transaction IDs
        normalized = Regex.Replace(normalized, @"\s+\d+$", "");

        // Clean up extra whitespace again
        normalized = Regex.Replace(normalized.Trim(), @"\s+", " ");

        return normalized;
    }

    private string FormatMerchantName(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return "Unknown Merchant";

        var normalized = NormalizeDescription(description);

        // Title case the result
        if (string.IsNullOrWhiteSpace(normalized))
            return "Unknown Merchant";

        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(normalized);
    }

    private decimal CalculateStringSimilarity(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) && string.IsNullOrEmpty(str2))
            return 1m;

        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
            return 0m;

        var distance = LevenshteinDistance(str1, str2);
        var maxLength = Math.Max(str1.Length, str2.Length);

        return 1m - (decimal)distance / maxLength;
    }

    private int LevenshteinDistance(string s1, string s2)
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
