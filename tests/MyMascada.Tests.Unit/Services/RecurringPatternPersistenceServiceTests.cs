using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.RecurringPatterns.Services;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using NSubstitute;

namespace MyMascada.Tests.Unit.Services;

public class RecurringPatternPersistenceServiceTests
{
    private readonly IRecurringPatternRepository _patternRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly RecurringPatternPersistenceService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private int _patternIdCounter = 1;

    public RecurringPatternPersistenceServiceTests()
    {
        _patternRepository = Substitute.For<IRecurringPatternRepository>();
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _service = new RecurringPatternPersistenceService(
            _patternRepository,
            _transactionRepository,
            Substitute.For<ILogger<RecurringPatternPersistenceService>>());
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_WithDuplicatePatternsForSameMerchant_ShouldReturnSingleBill()
    {
        // Arrange - three near-duplicate patterns for the same merchant (the reported bug):
        // same normalized key, near-identical amounts. They must surface as ONE bill.
        var today = DateTime.UtcNow.Date;
        var patterns = new List<RecurringPattern>
        {
            CreatePattern("ami insurance", amount: 42.74m, nextDue: today.AddDays(2), confidence: 0.80m, occurrences: 4),
            CreatePattern("ami insurance", amount: 42.75m, nextDue: today.AddDays(3), confidence: 0.85m, occurrences: 5),
            CreatePattern("ami insurance", amount: 42.75m, nextDue: today.AddDays(4), confidence: 0.82m, occurrences: 3),
        };

        _patternRepository
            .GetUpcomingAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(patterns);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, daysAhead: 7);

        // Assert
        result.Bills.Should().HaveCount(1, "duplicate patterns for one merchant must consolidate into a single bill");
        result.TotalBillsCount.Should().Be(1);
        result.Bills.Single().MerchantName.Should().Be("Ami Insurance");
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_WithDuplicatePatterns_ShouldKeepSoonestDue()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var patterns = new List<RecurringPattern>
        {
            CreatePattern("ami insurance", amount: 42.74m, nextDue: today.AddDays(5), confidence: 0.90m, occurrences: 4,
                lastObserved: today.AddDays(-25)),
            CreatePattern("ami insurance", amount: 42.99m, nextDue: today.AddDays(2), confidence: 0.80m, occurrences: 5,
                lastObserved: today.AddDays(-1)),
        };

        _patternRepository
            .GetUpcomingAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(patterns);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, daysAhead: 7);

        // Assert - representative is the soonest-due, with the most recent amount.
        result.Bills.Should().HaveCount(1);
        result.Bills.Single().DaysUntilDue.Should().Be(2);
        result.Bills.Single().ExpectedAmount.Should().Be(42.99m, "the most recently observed amount should win");
    }

    [Fact]
    public async Task GetUpcomingBillsAsync_WithDistinctMerchants_ShouldReturnSeparateBills()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var patterns = new List<RecurringPattern>
        {
            CreatePattern("ami insurance", amount: 42.75m, nextDue: today.AddDays(2), confidence: 0.85m, occurrences: 5),
            CreatePattern("netflix", amount: 15.99m, nextDue: today.AddDays(3), confidence: 0.90m, occurrences: 6),
        };

        _patternRepository
            .GetUpcomingAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(patterns);

        // Act
        var result = await _service.GetUpcomingBillsAsync(_userId, daysAhead: 7);

        // Assert
        result.Bills.Should().HaveCount(2);
    }

    [Fact]
    public async Task DetectAndPersistPatternsAsync_WithExistingDuplicatePatterns_ShouldMergeThem()
    {
        // Arrange - existing active patterns include three duplicates for one merchant.
        var today = DateTime.UtcNow.Date;
        var activePatterns = new List<RecurringPattern>
        {
            CreatePattern("ami insurance", amount: 42.74m, nextDue: today.AddDays(2), confidence: 0.80m, occurrences: 4,
                lastObserved: today.AddDays(-28)),
            CreatePattern("ami insurance", amount: 42.75m, nextDue: today.AddDays(3), confidence: 0.85m, occurrences: 5,
                lastObserved: today.AddDays(-1)),
            CreatePattern("ami insurance", amount: 42.75m, nextDue: today.AddDays(4), confidence: 0.82m, occurrences: 3,
                lastObserved: today.AddDays(-30)),
        };

        _patternRepository.GetActiveAsync(_userId, Arg.Any<CancellationToken>()).Returns(activePatterns);
        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(new List<Transaction>());

        // Act
        await _service.DetectAndPersistPatternsAsync(_userId);

        // Assert - reconciliation merges the two duplicates into the canonical (most occurrences)
        // pattern, summing occurrence counts and adopting the freshest amount/dates.
        await _patternRepository.Received(1).MergeDuplicatePatternsAsync(
            _userId,
            canonicalPatternId: activePatterns[1].Id, // occurrences:5 -> canonical
            Arg.Is<IEnumerable<int>>(ids => ids.OrderBy(x => x).SequenceEqual(
                new[] { activePatterns[0].Id, activePatterns[2].Id }.OrderBy(x => x))),
            mergedNormalizedKey: "ami insurance",
            mergedOccurrenceCount: 12, // 4 + 5 + 3
            mergedAverageAmount: 42.75m, // most recent (lastObserved -1)
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DetectAndPersistPatternsAsync_WithStaleNormalizedKey_ShouldMigrateLoneRowKey()
    {
        // Arrange - a single active pattern whose stored key was produced by the old normalizer.
        var today = DateTime.UtcNow.Date;
        var stale = CreatePattern("ami insurance amiinsurance 3301234", amount: 42.75m,
            nextDue: today.AddDays(3), confidence: 0.85m, occurrences: 5);

        _patternRepository.GetActiveAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<RecurringPattern> { stale });
        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(new List<Transaction>());

        // Act
        await _service.DetectAndPersistPatternsAsync(_userId);

        // Assert - the lone stale row's key is migrated to the clean normalized form so the next
        // exact-key upsert updates it rather than creating a fresh duplicate.
        await _patternRepository.Received(1).UpdateNormalizedKeyAsync(
            _userId, stale.Id, "ami insurance", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DetectAndPersistPatternsAsync_WithAlreadyCleanLoneKey_ShouldNotMigrate()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var clean = CreatePattern("ami insurance", amount: 42.75m,
            nextDue: today.AddDays(3), confidence: 0.85m, occurrences: 5);

        _patternRepository.GetActiveAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<RecurringPattern> { clean });
        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(new List<Transaction>());

        // Act
        await _service.DetectAndPersistPatternsAsync(_userId);

        // Assert
        await _patternRepository.DidNotReceive().UpdateNormalizedKeyAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DetectAndPersistPatternsAsync_WithDistinctMerchants_ShouldNotMerge()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var activePatterns = new List<RecurringPattern>
        {
            CreatePattern("ami insurance", amount: 42.75m, nextDue: today.AddDays(2), confidence: 0.85m, occurrences: 5),
            CreatePattern("netflix", amount: 15.99m, nextDue: today.AddDays(3), confidence: 0.90m, occurrences: 6),
        };

        _patternRepository.GetActiveAsync(_userId, Arg.Any<CancellationToken>()).Returns(activePatterns);
        _transactionRepository.GetByDateRangeAsync(_userId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(new List<Transaction>());

        // Act
        await _service.DetectAndPersistPatternsAsync(_userId);

        // Assert - no merge should occur for genuinely distinct merchants.
        await _patternRepository.DidNotReceive().MergeDuplicatePatternsAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<IEnumerable<int>>(),
            Arg.Any<string>(), Arg.Any<int>(), Arg.Any<decimal>(), Arg.Any<DateTime>(),
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    private RecurringPattern CreatePattern(
        string normalizedKey,
        decimal amount,
        DateTime nextDue,
        decimal confidence,
        int occurrences,
        DateTime? lastObserved = null)
    {
        return new RecurringPattern
        {
            Id = _patternIdCounter++,
            UserId = _userId,
            MerchantName = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalizedKey),
            NormalizedMerchantKey = normalizedKey,
            IntervalDays = 30,
            AverageAmount = amount,
            Confidence = confidence,
            Status = RecurringPatternStatus.Active,
            NextExpectedDate = nextDue,
            LastObservedAt = lastObserved ?? DateTime.UtcNow.AddDays(-27),
            OccurrenceCount = occurrences,
            ConsecutiveMisses = 0
        };
    }
}
