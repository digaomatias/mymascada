using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Services;
using MyMascada.Domain.Entities;

namespace MyMascada.Tests.Unit.Services;

public class SimilarityMatchingServiceTests
{
    private readonly ICategorizationHistoryRepository _historyRepo;
    private readonly SimilarityMatchingService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public SimilarityMatchingServiceTests()
    {
        _historyRepo = Substitute.For<ICategorizationHistoryRepository>();
        _service = new SimilarityMatchingService(
            _historyRepo,
            Substitute.For<ILogger<SimilarityMatchingService>>());
    }

    [Fact]
    public async Task FindBestMatch_EmptyDescription_ReturnsNull()
    {
        var result = await _service.FindBestMatchAsync(_userId, "");
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindBestMatch_ExactMatch_ReturnsWithCorrectConfidence()
    {
        var history = new CategorizationHistory
        {
            UserId = _userId,
            NormalizedDescription = "netflix com",
            CategoryId = 5,
            MatchCount = 3,
            Category = new Category { Id = 5, Name = "Entertainment" }
        };
        _historyRepo.FindByNormalizedDescriptionAsync(_userId, "netflix com", Arg.Any<CancellationToken>())
            .Returns(history);

        var result = await _service.FindBestMatchAsync(_userId, "netflix com");

        result.Should().NotBeNull();
        result!.CategoryId.Should().Be(5);
        result.CategoryName.Should().Be("Entertainment");
        result.MatchType.Should().Be("Exact");
        result.Confidence.Should().Be(0.85m); // matchCount 3 → 0.85
    }

    [Fact]
    public async Task FindBestMatch_ExactMatch_10Uses_HighConfidence()
    {
        var history = new CategorizationHistory
        {
            UserId = _userId,
            NormalizedDescription = "netflix com",
            CategoryId = 5,
            MatchCount = 10,
            Category = new Category { Id = 5, Name = "Entertainment" }
        };
        _historyRepo.FindByNormalizedDescriptionAsync(_userId, "netflix com", Arg.Any<CancellationToken>())
            .Returns(history);

        var result = await _service.FindBestMatchAsync(_userId, "netflix com");

        result.Should().NotBeNull();
        result!.Confidence.Should().Be(0.95m); // matchCount 10 → 0.95
    }

    [Fact]
    public async Task FindBestMatch_FuzzyMatch_TokenOverlap()
    {
        // No exact match
        _historyRepo.FindByNormalizedDescriptionAsync(_userId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((CategorizationHistory?)null);

        // User has history with similar description — high overlap with query
        var history = new List<CategorizationHistory>
        {
            new()
            {
                UserId = _userId,
                NormalizedDescription = "countdown supermarket petone",
                CategoryId = 10,
                MatchCount = 10, // high match count → 0.95 base confidence
                Category = new Category { Id = 10, Name = "Groceries" }
            }
        };
        _historyRepo.GetAllForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(history);

        // Query shares 2 of 3 significant tokens: "countdown", "petone"
        // Token overlap = 2/3 = 0.667, base confidence = 0.95, final = 0.633 > 0.60
        var result = await _service.FindBestMatchAsync(_userId, "countdown petone store");

        result.Should().NotBeNull();
        result!.CategoryId.Should().Be(10);
        result.MatchType.Should().Be("Fuzzy");
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.60m);
    }

    [Fact]
    public async Task FindBestMatch_NoMatch_ReturnsNull()
    {
        _historyRepo.FindByNormalizedDescriptionAsync(_userId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((CategorizationHistory?)null);
        _historyRepo.GetAllForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<CategorizationHistory>());

        var result = await _service.FindBestMatchAsync(_userId, "completely unknown merchant");
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindBestMatch_FuzzyMatch_BelowThreshold_ReturnsNull()
    {
        _historyRepo.FindByNormalizedDescriptionAsync(_userId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((CategorizationHistory?)null);

        // Only 1 shared token out of many → low confidence
        var history = new List<CategorizationHistory>
        {
            new()
            {
                UserId = _userId,
                NormalizedDescription = "countdown wellington lower hutt groceries",
                CategoryId = 10,
                MatchCount = 1,
                Category = new Category { Id = 10, Name = "Groceries" }
            }
        };
        _historyRepo.GetAllForUserAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(history);

        // Only shares "wellington" — too few shared tokens or too low overlap
        var result = await _service.FindBestMatchAsync(_userId, "wellington airport parking fees");

        // With only 1 shared significant token out of 3+ on each side,
        // and matchCount=1 (base confidence 0.70), the score should be low
        // but fuzzy matching requires >= 2 shared tokens, so this should return null
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(1, 0.70)]
    [InlineData(2, 0.80)]
    [InlineData(3, 0.85)]
    [InlineData(4, 0.85)]
    [InlineData(5, 0.90)]
    [InlineData(9, 0.90)]
    [InlineData(10, 0.95)]
    [InlineData(100, 0.95)]
    public void CalculateConfidence_ScalesCorrectly(int matchCount, decimal expected)
    {
        SimilarityMatchingService.CalculateConfidence(matchCount).Should().Be(expected);
    }
}
