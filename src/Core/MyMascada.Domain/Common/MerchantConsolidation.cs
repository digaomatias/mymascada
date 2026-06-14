using MyMascada.Domain.Entities;

namespace MyMascada.Domain.Common;

/// <summary>
/// Read-path consolidation of recurring patterns that resolve to the same merchant.
///
/// This is a display-time safety net: even if duplicate <see cref="RecurringPattern"/> rows
/// still exist for one merchant, the user should only ever see a single upcoming bill. It is
/// deliberately pure — it never mutates the supplied entities (which are typically tracked by
/// the EF Core DbContext), returning the chosen representative plus a separate display amount.
/// </summary>
public static class MerchantConsolidation
{
    /// <summary>
    /// A consolidated upcoming bill: the representative pattern and the amount to display
    /// (the most recently observed amount across the duplicate group).
    /// </summary>
    public readonly record struct ConsolidatedPattern(RecurringPattern Pattern, decimal DisplayAmount);

    /// <summary>
    /// Collapses patterns that resolve to the same merchant (identical normalized key, or
    /// near-duplicate via the shared similarity rule) into a single representative each.
    /// The representative is the soonest-due, then highest-confidence pattern; the display
    /// amount is taken from the most recently observed pattern in the group.
    /// </summary>
    public static List<ConsolidatedPattern> ConsolidateUpcoming(
        IEnumerable<RecurringPattern> patterns,
        DateTime today)
    {
        var patternList = patterns.ToList();
        if (patternList.Count == 0)
            return new List<ConsolidatedPattern>();

        if (patternList.Count == 1)
        {
            return new List<ConsolidatedPattern>
            {
                new(patternList[0], patternList[0].AverageAmount)
            };
        }

        // Re-normalize stored keys before grouping so rows persisted by an older normalizer
        // (which may still contain reference tokens or doubled words) collapse with cleaner ones.
        var normalizedByPattern = patternList.ToDictionary(
            p => p,
            p => MerchantNormalizer.Normalize(p.NormalizedMerchantKey));

        var canonicalByKey = MerchantNormalizer.GroupSimilarKeys(
            normalizedByPattern.Values.ToList());

        var grouped = patternList.GroupBy(p =>
            canonicalByKey.TryGetValue(normalizedByPattern[p], out var canonical)
                ? canonical
                : normalizedByPattern[p]);

        var result = new List<ConsolidatedPattern>();
        foreach (var group in grouped)
        {
            var representative = group
                .OrderBy(p => p.GetDaysUntilDue(today))
                .ThenByDescending(p => p.Confidence)
                .First();

            var displayAmount = group
                .OrderByDescending(p => p.LastObservedAt)
                .First()
                .AverageAmount;

            result.Add(new ConsolidatedPattern(representative, displayAmount));
        }

        return result;
    }
}
