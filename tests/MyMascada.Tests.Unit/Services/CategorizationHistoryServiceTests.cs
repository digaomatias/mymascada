using Microsoft.Extensions.Logging;
using MyMascada.Application.Common.Interfaces;
using MyMascada.Application.Features.Categorization.Services;
using MyMascada.Domain.Entities;

namespace MyMascada.Tests.Unit.Services;

public class CategorizationHistoryServiceTests
{
    private readonly ICategorizationHistoryRepository _historyRepo;
    private readonly CategorizationHistoryService _service;
    private readonly Guid _userId = Guid.NewGuid();

    public CategorizationHistoryServiceTests()
    {
        _historyRepo = Substitute.For<ICategorizationHistoryRepository>();
        _service = new CategorizationHistoryService(
            _historyRepo,
            Substitute.For<ILogger<CategorizationHistoryService>>());
    }

    [Fact]
    public async Task RecordCategorization_NormalizesAndUpserts()
    {
        _historyRepo.UpsertAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<CategorizationHistorySource>(), Arg.Any<CancellationToken>())
            .Returns(new CategorizationHistory { MatchCount = 1 });

        await _service.RecordCategorizationAsync(
            _userId, "PAK N SAVE PETONE NZ 15/03/2026", 10, CategorizationHistorySource.Manual);

        await _historyRepo.Received(1).UpsertAsync(
            _userId,
            "pak n save petone nz", // normalized
            "PAK N SAVE PETONE NZ 15/03/2026", // original
            10,
            CategorizationHistorySource.Manual,
            Arg.Any<CancellationToken>());
        await _historyRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordCategorization_EmptyDescription_DoesNotUpsert()
    {
        await _service.RecordCategorizationAsync(_userId, "", 10, CategorizationHistorySource.Manual);

        await _historyRepo.DidNotReceive().UpsertAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<CategorizationHistorySource>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordCategorization_RepositoryThrows_DoesNotPropagate()
    {
        _historyRepo.UpsertAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int>(), Arg.Any<CategorizationHistorySource>(), Arg.Any<CancellationToken>())
            .Returns<CategorizationHistory>(x => throw new InvalidOperationException("DB error"));

        // Should not throw — history recording is non-critical
        var act = () => _service.RecordCategorizationAsync(
            _userId, "NETFLIX.COM", 5, CategorizationHistorySource.Manual);

        await act.Should().NotThrowAsync();
    }
}
