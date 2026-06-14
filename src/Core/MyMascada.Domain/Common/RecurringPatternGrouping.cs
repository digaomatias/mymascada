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
    /// Whether two patterns are confidently the same recurring bill. Requires BOTH:
    ///  - a merchant match (callers pre-group by name similarity; an exact normalized-key match
    ///    also qualifies), AND
    ///  - corroborating evidence: the same cadence and amounts within <see cref="AmountTolerance"/>.
    ///
    /// The evidence check applies even to exact normalized-key matches, because the normalizer
    /// strips numeric policy/account IDs — so two genuinely-different bills at one merchant (e.g.
    /// two insurance policies with different amounts) can normalize to the same key and must NOT
    /// be collapsed. Conversely, name similarity alone is never sufficient.
    /// </summary>
    public static bool AreSameRecurringBill(RecurringPattern a, RecurringPattern b)
    {
        // Callers pass members of the same name-similarity group (an exact cleaned-key match is
        // the strongest form of that). But the key alone is NOT decisive — the normalizer strips
        // policy/account IDs, so two distinct bills at one merchant can share a key — hence
        // corroborating cadence + amount evidence is always required.
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
    public static List<List<RecurringPattern>> PartitionBySameBill(List<RecurringPattern> members)
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
                if (AreSameRecurringBill(members[i], members[j]))
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
