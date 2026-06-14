using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.UpcomingBills.DTOs;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Application.Features.RecurringPatterns.Services;

/// <summary>
/// Service for persisting and managing recurring payment patterns.
/// Handles pattern detection, status transitions, and occurrence tracking.
/// </summary>
public interface IRecurringPatternPersistenceService
{
    /// <summary>
    /// Detects and persists recurring patterns for a user from their transaction history
    /// </summary>
    Task<int> DetectAndPersistPatternsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes missed payments and updates pattern statuses
    /// </summary>
    Task<int> ProcessMissedPaymentsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Matches a transaction to an existing pattern and records the occurrence
    /// </summary>
    Task<bool> TryMatchTransactionToPatternAsync(
        Transaction transaction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets upcoming bills from persisted patterns for a user
    /// </summary>
    Task<UpcomingBillsResponse> GetUpcomingBillsAsync(
        Guid userId,
        int daysAhead = 7,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of the recurring pattern persistence service
/// </summary>
public class RecurringPatternPersistenceService : IRecurringPatternPersistenceService
{
    private readonly IRecurringPatternRepository _patternRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<RecurringPatternPersistenceService> _logger;

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

    public RecurringPatternPersistenceService(
        IRecurringPatternRepository patternRepository,
        ITransactionRepository transactionRepository,
        ILogger<RecurringPatternPersistenceService> logger)
    {
        _patternRepository = patternRepository;
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    /// <summary>
    /// Detects and persists recurring patterns for a user from their transaction history
    /// </summary>
    public async Task<int> DetectAndPersistPatternsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Self-heal: merge any already-persisted duplicate patterns for this user so existing
        // accounts (which may already have several stale rows for the same merchant) converge
        // to a single canonical pattern. Runs every detection pass via the background job.
        await ReconcileDuplicatePatternsAsync(userId, cancellationToken);

        var today = DateTime.UtcNow.Date;
        var startDate = today.AddMonths(-LookbackMonths);

        // Get expenses from last 6 months (negative amounts = expenses)
        var transactions = await _transactionRepository.GetByDateRangeAsync(userId, startDate, today);
        var expenses = transactions
            .Where(t => t.Amount < 0 && !t.IsDeleted && t.TransferId == null)
            .ToList();

        if (!expenses.Any())
        {
            _logger.LogDebug("No expenses found for user {UserId} in the last {Months} months", userId, LookbackMonths);
            return 0;
        }

        // Group by normalized description
        var groupedTransactions = GroupByNormalizedDescription(expenses);

        var patternsCreatedOrUpdated = 0;

        foreach (var group in groupedTransactions)
        {
            if (group.Value.Count < MinOccurrencesForPattern)
                continue;

            var detectedPattern = DetectPattern(group.Key, group.Value, today);
            if (detectedPattern != null && detectedPattern.Confidence >= MinConfidenceThreshold)
            {
                try
                {
                    detectedPattern.UserId = userId;
                    await _patternRepository.UpsertAsync(detectedPattern, cancellationToken);
                    patternsCreatedOrUpdated++;

                    _logger.LogDebug("Upserted recurring pattern for {MerchantName} with confidence {Confidence}",
                        detectedPattern.MerchantName, detectedPattern.Confidence);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upsert pattern for merchant {MerchantKey}", group.Key);
                }
            }
        }

        _logger.LogInformation("Detected and persisted {Count} recurring patterns for user {UserId}",
            patternsCreatedOrUpdated, userId);

        return patternsCreatedOrUpdated;
    }

    /// <summary>
    /// Processes missed payments and updates pattern statuses
    /// </summary>
    public async Task<int> ProcessMissedPaymentsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var pastDuePatterns = await _patternRepository.GetPastDueAsync(userId, today, cancellationToken);
        var processedCount = 0;

        foreach (var pattern in pastDuePatterns)
        {
            try
            {
                // Record the miss
                pattern.RecordMiss(pattern.NextExpectedDate);

                // Create a missed occurrence record
                var missedOccurrence = RecurringOccurrence.CreateMissed(
                    pattern.Id,
                    pattern.NextExpectedDate,
                    pattern.AverageAmount);

                await _patternRepository.CreateOccurrenceAsync(missedOccurrence, cancellationToken);

                // Calculate next expected date
                pattern.NextExpectedDate = pattern.LastObservedAt.AddDays(pattern.IntervalDays * (pattern.ConsecutiveMisses + 1));

                await _patternRepository.UpdateAsync(pattern, cancellationToken);
                processedCount++;

                _logger.LogInformation("Recorded missed payment for pattern {PatternId} ({MerchantName}). " +
                    "ConsecutiveMisses: {ConsecutiveMisses}, Status: {Status}",
                    pattern.Id, pattern.MerchantName, pattern.ConsecutiveMisses, pattern.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process missed payment for pattern {PatternId}", pattern.Id);
            }
        }

        _logger.LogInformation("Processed {Count} missed payments for user {UserId}", processedCount, userId);

        return processedCount;
    }

    /// <summary>
    /// Matches a transaction to an existing pattern and records the occurrence
    /// </summary>
    public async Task<bool> TryMatchTransactionToPatternAsync(
        Transaction transaction,
        CancellationToken cancellationToken = default)
    {
        // Only match expenses (negative amounts)
        if (transaction.Amount >= 0 || transaction.TransferId != null)
            return false;

        // Check if this transaction is already linked
        if (await _patternRepository.IsTransactionLinkedAsync(transaction.Id, cancellationToken))
            return false;

        // Get active patterns for the user (from the account's user)
        var userId = transaction.Account?.UserId ?? Guid.Empty;
        if (userId == Guid.Empty)
            return false;

        var activePatterns = await _patternRepository.GetActiveAsync(userId, cancellationToken);

        foreach (var pattern in activePatterns)
        {
            if (pattern.MatchesTransaction(transaction.Description, transaction.Amount))
            {
                try
                {
                    // Determine if this is late or on time
                    var wasLate = transaction.TransactionDate.Date > pattern.NextExpectedDate.Date;

                    // Create occurrence record
                    var occurrence = RecurringOccurrence.CreatePosted(
                        pattern.Id,
                        pattern.NextExpectedDate,
                        pattern.AverageAmount,
                        transaction.Id,
                        transaction.TransactionDate,
                        Math.Abs(transaction.Amount));

                    await _patternRepository.CreateOccurrenceAsync(occurrence, cancellationToken);

                    // Update the pattern
                    pattern.RecordMatch(transaction.TransactionDate, transaction.Amount);
                    await _patternRepository.UpdateAsync(pattern, cancellationToken);

                    _logger.LogInformation("Matched transaction {TransactionId} to pattern {PatternId} ({MerchantName}). " +
                        "Next expected date: {NextDate}",
                        transaction.Id, pattern.Id, pattern.MerchantName, pattern.NextExpectedDate);

                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to match transaction {TransactionId} to pattern {PatternId}",
                        transaction.Id, pattern.Id);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Gets upcoming bills from persisted patterns for a user
    /// </summary>
    public async Task<UpcomingBillsResponse> GetUpcomingBillsAsync(
        Guid userId,
        int daysAhead = 7,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var endDate = today.AddDays(daysAhead);

        // Get upcoming patterns from persisted data
        var upcomingPatterns = await _patternRepository.GetUpcomingAsync(userId, today, endDate, cancellationToken);

        if (!upcomingPatterns.Any())
        {
            return new UpcomingBillsResponse
            {
                Bills = new List<UpcomingBillDto>(),
                TotalBillsCount = 0,
                TotalExpectedAmount = 0
            };
        }

        // Safety net: even if duplicate pattern rows still exist for the same merchant
        // (e.g. before the reconciliation pass has run), consolidate them here so the user
        // never sees the same merchant more than once. Keep the soonest-due / highest-confidence
        // pattern and prefer the most recently observed amount.
        var consolidated = MerchantConsolidation.ConsolidateUpcoming(upcomingPatterns, today);

        var bills = consolidated.Select(c => new UpcomingBillDto
        {
            PatternId = c.Pattern.Id,
            MerchantName = c.Pattern.MerchantName,
            ExpectedAmount = Math.Round(c.DisplayAmount, 2),
            ExpectedDate = c.Pattern.NextExpectedDate,
            DaysUntilDue = c.Pattern.GetDaysUntilDue(today),
            ConfidenceScore = Math.Round(c.Pattern.Confidence, 2),
            ConfidenceLevel = c.Pattern.GetConfidenceLevel(),
            Interval = c.Pattern.GetIntervalName(),
            OccurrenceCount = c.Pattern.OccurrenceCount
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

    #region Private Helper Methods

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

    private static Dictionary<string, List<Transaction>> MergeSimilarGroups(Dictionary<string, List<Transaction>> groups)
    {
        if (groups.Count <= 1)
            return groups;

        // Transitive grouping: union-find collapses A~B~C into one group even when A is not
        // directly similar to C. This is the key fix for the same-merchant-split-into-many bug.
        var canonicalByKey = MerchantNormalizer.GroupSimilarKeys(groups.Keys.ToList());

        var mergedGroups = new Dictionary<string, List<Transaction>>();
        var componentMembers = new Dictionary<string, List<string>>();

        foreach (var key in groups.Keys)
        {
            var canonical = canonicalByKey.TryGetValue(key, out var c) ? c : key;
            if (!componentMembers.TryGetValue(canonical, out var members))
            {
                members = new List<string>();
                componentMembers[canonical] = members;
            }
            members.Add(key);
        }

        foreach (var (_, members) in componentMembers)
        {
            // Choose the display key as the member with the most transactions (most common
            // representation of the merchant), so the resulting pattern uses a sensible key.
            var displayKey = members
                .OrderByDescending(k => groups[k].Count)
                .ThenBy(k => k, StringComparer.Ordinal)
                .First();

            var mergedList = members.SelectMany(k => groups[k]).ToList();
            mergedGroups[displayKey] = mergedList;
        }

        return mergedGroups;
    }

    /// <summary>
    /// Merges already-persisted duplicate <see cref="RecurringPattern"/> rows for a user.
    /// Active patterns are grouped by normalized/similar merchant key; one canonical pattern
    /// is kept (most occurrences, then highest confidence) with merged occurrence counts and
    /// the most recent amount/next-expected-date, and the rest are soft-deleted.
    /// </summary>
    private async Task ReconcileDuplicatePatternsAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var activePatterns = (await _patternRepository.GetActiveAsync(userId, cancellationToken)).ToList();
            if (activePatterns.Count <= 1)
                return;

            // Re-normalize the stored keys first: rows persisted by an older normalizer may
            // still carry reference tokens or doubled words (e.g. "ami insurance amiinsurance"),
            // which must be cleaned before grouping so they collapse with freshly-detected keys.
            var normalizedByPattern = activePatterns.ToDictionary(
                p => p,
                p => MerchantNormalizer.Normalize(p.NormalizedMerchantKey));

            var canonicalByKey = MerchantNormalizer.GroupSimilarKeys(
                normalizedByPattern.Values.ToList());

            var groups = activePatterns
                .GroupBy(p => canonicalByKey.TryGetValue(normalizedByPattern[p], out var canonical)
                    ? canonical
                    : normalizedByPattern[p])
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var group in groups)
            {
                var members = group.ToList();

                // Keep the strongest pattern: most occurrences, then highest confidence.
                var canonicalPattern = members
                    .OrderByDescending(p => p.OccurrenceCount)
                    .ThenByDescending(p => p.Confidence)
                    .First();

                var duplicateIds = members.Where(p => p.Id != canonicalPattern.Id).Select(p => p.Id).ToList();

                // Merge occurrence history (counts) and adopt the freshest observation/amount.
                var mostRecent = members.OrderByDescending(p => p.LastObservedAt).First();

                await _patternRepository.MergeDuplicatePatternsAsync(
                    userId,
                    canonicalPattern.Id,
                    duplicateIds,
                    mergedOccurrenceCount: members.Sum(p => p.OccurrenceCount),
                    mergedAverageAmount: mostRecent.AverageAmount,
                    mergedLastObservedAt: mostRecent.LastObservedAt,
                    mergedNextExpectedDate: mostRecent.NextExpectedDate,
                    cancellationToken);

                _logger.LogInformation(
                    "Reconciled {DuplicateCount} duplicate recurring pattern(s) into {CanonicalId} for merchant {MerchantName}",
                    duplicateIds.Count, canonicalPattern.Id, canonicalPattern.MerchantName);
            }
        }
        catch (Exception ex)
        {
            // Reconciliation is best-effort self-healing; never let it break detection.
            _logger.LogError(ex, "Failed to reconcile duplicate recurring patterns for user {UserId}", userId);
        }
    }

    private RecurringPattern? DetectPattern(string merchantKey, List<Transaction> transactions, DateTime today)
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
        var detectedIntervalDays = DetectRecurrenceIntervalDays(averageInterval);

        if (detectedIntervalDays == null)
            return null;

        // Calculate expected amount (average of all amounts, made positive)
        var amounts = sortedTransactions.Select(t => Math.Abs(t.Amount)).ToList();
        var averageAmount = amounts.Average();

        // Calculate confidence score
        var confidence = CalculateConfidence(intervals, amounts, detectedIntervalDays.Value);

        if (confidence < MinConfidenceThreshold)
            return null;

        // Get last transaction and calculate next expected date
        var lastTransaction = sortedTransactions.Last();
        var nextExpectedDate = lastTransaction.TransactionDate.Date.AddDays(detectedIntervalDays.Value);

        // Format merchant name (use most recent transaction's description)
        var displayName = FormatMerchantName(lastTransaction.Description);

        return new RecurringPattern
        {
            MerchantName = displayName,
            NormalizedMerchantKey = merchantKey,
            IntervalDays = detectedIntervalDays.Value,
            AverageAmount = Math.Round(averageAmount, 2),
            Confidence = Math.Round(confidence, 4),
            Status = RecurringPatternStatus.Active,
            NextExpectedDate = nextExpectedDate,
            LastObservedAt = lastTransaction.TransactionDate,
            OccurrenceCount = sortedTransactions.Count,
            ConsecutiveMisses = 0
        };
    }

    private int? DetectRecurrenceIntervalDays(double averageInterval)
    {
        if (averageInterval >= WeeklyMinDays && averageInterval <= WeeklyMaxDays)
            return (int)RecurrenceInterval.Weekly;
        if (averageInterval >= BiweeklyMinDays && averageInterval <= BiweeklyMaxDays)
            return (int)RecurrenceInterval.Biweekly;
        if (averageInterval >= MonthlyMinDays && averageInterval <= MonthlyMaxDays)
            return (int)RecurrenceInterval.Monthly;

        return null;
    }

    private decimal CalculateConfidence(List<int> intervals, List<decimal> amounts, int expectedInterval)
    {
        // Weights: occurrences (40%) + interval consistency (35%) + amount consistency (25%)
        decimal confidenceScore = 0m;

        // 1. Occurrence count (40% weight)
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
        var intervalWeight = 0.35m;
        if (intervals.Any())
        {
            var intervalDeviation = intervals.Select(i => Math.Abs(i - expectedInterval)).Average();
            var maxDeviation = expectedInterval * 0.3; // Allow 30% deviation
            var intervalScore = Math.Max(0, 1 - (decimal)(intervalDeviation / maxDeviation));
            confidenceScore += intervalWeight * intervalScore;
        }

        // 3. Amount consistency (25% weight)
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
            confidenceScore += amountWeight * 0.5m;
        }

        return Math.Min(1.0m, confidenceScore);
    }

    private static string NormalizeDescription(string? description)
        => MerchantNormalizer.Normalize(description);

    private static string FormatMerchantName(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return "Unknown Merchant";

        var normalized = NormalizeDescription(description);

        // Title case the result
        if (string.IsNullOrWhiteSpace(normalized))
            return "Unknown Merchant";

        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(normalized);
    }

    #endregion
}
