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
    /// Collapses patterns that are confidently the same recurring bill into a single representative
    /// each. Patterns are first grouped by merchant-name similarity, then each name-group is split
    /// by corroborating evidence (exact normalized key, or same cadence + close amount) so two
    /// distinct merchants with similar names are NOT collapsed into one — which would hide a real
    /// bill from the dashboard. The representative is the soonest-due, then highest-confidence
    /// pattern; the display amount is the most recently observed amount in the cluster.
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

        var nameGroups = patternList.GroupBy(p =>
            canonicalByKey.TryGetValue(normalizedByPattern[p], out var canonical)
                ? canonical
                : normalizedByPattern[p]);

        var result = new List<ConsolidatedPattern>();
        foreach (var nameGroup in nameGroups)
        {
            // Only collapse rows that are confidently the same bill; keep distinct similarly-named
            // merchants as separate dashboard entries.
            var clusters = RecurringPatternGrouping.PartitionBySameBill(nameGroup.ToList());

            foreach (var cluster in clusters)
            {
                var representative = cluster
                    .OrderBy(p => p.GetDaysUntilDue(today))
                    .ThenByDescending(p => p.Confidence)
                    .First();

                var displayAmount = cluster
                    .OrderByDescending(p => p.LastObservedAt)
                    .First()
                    .AverageAmount;

                result.Add(new ConsolidatedPattern(representative, displayAmount));
            }
        }

        return result;
    }
}
