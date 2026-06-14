using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;

namespace MyMascada.Tests.Unit.Domain;

public class RecurringPatternGroupingTests
{
    [Fact]
    public void PartitionBySameBill_ExactKeyMatch_AlwaysClustersTogether()
    {
        var members = new List<RecurringPattern>
        {
            Pattern("ami insurance", intervalDays: 30, amount: 42.74m),
            Pattern("ami insurance", intervalDays: 30, amount: 99.00m), // wildly different amount
        };

        var clusters = RecurringPatternGrouping.PartitionBySameBill(members, p => p.NormalizedMerchantKey);

        clusters.Should().HaveCount(1, "exact normalized-key matches are unambiguous duplicates");
    }

    [Fact]
    public void PartitionBySameBill_FuzzyNameDifferentCadenceAndAmount_StaysSeparate()
    {
        var members = new List<RecurringPattern>
        {
            Pattern("acme services", intervalDays: 7, amount: 12.00m),
            Pattern("acme service", intervalDays: 30, amount: 250.00m),
        };

        var clusters = RecurringPatternGrouping.PartitionBySameBill(members, p => p.NormalizedMerchantKey);

        clusters.Should().HaveCount(2, "similar names alone must not collapse distinct bills");
    }

    [Fact]
    public void PartitionBySameBill_FuzzyNameSameCadenceAndCloseAmount_Clusters()
    {
        var members = new List<RecurringPattern>
        {
            Pattern("acme services", intervalDays: 30, amount: 100.00m),
            Pattern("acme service", intervalDays: 30, amount: 110.00m), // within ±20%
        };

        var clusters = RecurringPatternGrouping.PartitionBySameBill(members, p => p.NormalizedMerchantKey);

        clusters.Should().HaveCount(1);
    }

    [Fact]
    public void PartitionBySameBill_ChainAxBxC_SplitsOutDistinctMember()
    {
        // A and B share an exact key (same bill); C has a similar name but a very different
        // cadence + amount, so it must NOT be pulled into the A/B cluster.
        var members = new List<RecurringPattern>
        {
            Pattern("acme services", intervalDays: 30, amount: 100.00m), // A
            Pattern("acme services", intervalDays: 30, amount: 100.00m), // B (exact key with A)
            Pattern("acme service", intervalDays: 7, amount: 9.00m),     // C (distinct bill)
        };

        var clusters = RecurringPatternGrouping.PartitionBySameBill(members, p => p.NormalizedMerchantKey);

        clusters.Should().HaveCount(2);
        clusters.Should().ContainSingle(c => c.Count == 2);
        clusters.Should().ContainSingle(c => c.Count == 1);
    }

    private static RecurringPattern Pattern(string key, int intervalDays, decimal amount)
        => new()
        {
            Id = Random.Shared.Next(1, int.MaxValue),
            UserId = Guid.NewGuid(),
            MerchantName = key,
            NormalizedMerchantKey = key,
            IntervalDays = intervalDays,
            AverageAmount = amount,
            Confidence = 0.8m,
            Status = RecurringPatternStatus.Active,
            NextExpectedDate = DateTime.UtcNow.AddDays(3),
            LastObservedAt = DateTime.UtcNow.AddDays(-27),
            OccurrenceCount = 4
        };
}
