using MyMascada.Domain.Entities;

namespace MyMascada.Domain.Common;

/// <summary>
/// Shared grouping rules for deciding when two persisted <see cref="RecurringPattern"/> rows are
/// confidently the SAME recurring bill (and may therefore be consolidated/merged), used by both
/// the read-path display consolidation and the write-path reconciliation so the two never drift.
/// </summary>
public static class RecurringPatternGrouping
{
    /// <summary>Amount tolerance for treating two fuzzy-name-matched patterns as the same bill.</summary>
    public const decimal AmountTolerance = 0.2m; // ±20%

    /// <summary>
    /// Whether two patterns are confidently the same recurring bill: an exact normalized-key
    /// match, or a fuzzy-name match (callers pre-group by name similarity) backed by the same
    /// cadence and amounts within <see cref="AmountTolerance"/>. Name similarity alone is not
    /// sufficient — two distinct merchants can have near-identical names.
    /// </summary>
    public static bool AreSameRecurringBill(
        RecurringPattern a,
        RecurringPattern b,
        string normalizedKeyA,
        string normalizedKeyB)
    {
        if (string.Equals(normalizedKeyA, normalizedKeyB, StringComparison.Ordinal))
            return true;

        var sameCadence = a.GetIntervalName() == b.GetIntervalName();

        var larger = Math.Max(a.AverageAmount, b.AverageAmount);
        var smaller = Math.Min(a.AverageAmount, b.AverageAmount);
        var closeAmount = larger <= 0
            ? smaller <= 0
            : (larger - smaller) <= larger * AmountTolerance;

        return sameCadence && closeAmount;
    }

    /// <summary>
    /// Partitions a name-similarity group into clusters that are confidently the same recurring
    /// bill, via union-find over <see cref="AreSameRecurringBill"/> (transitive within the group).
    /// </summary>
    public static List<List<RecurringPattern>> PartitionBySameBill(
        List<RecurringPattern> members,
        Func<RecurringPattern, string> normalizedKeyOf)
    {
        if (members.Count <= 1)
            return new List<List<RecurringPattern>> { members };

        var parent = Enumerable.Range(0, members.Count).ToArray();

        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
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

        for (var i = 0; i < members.Count; i++)
        {
            for (var j = i + 1; j < members.Count; j++)
            {
                if (AreSameRecurringBill(members[i], members[j], normalizedKeyOf(members[i]), normalizedKeyOf(members[j])))
                    Union(i, j);
            }
        }

        return members
            .Select((pattern, index) => (pattern, root: Find(index)))
            .GroupBy(x => x.root)
            .Select(g => g.Select(x => x.pattern).ToList())
            .ToList();
    }
}
