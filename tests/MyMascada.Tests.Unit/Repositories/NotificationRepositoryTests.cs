using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMascada.Domain.Entities;
using MyMascada.Domain.Enums;
using MyMascada.Infrastructure.Data;
using MyMascada.Infrastructure.Repositories;

namespace MyMascada.Tests.Unit.Repositories;

/// <summary>
/// Exercises ExistsByGroupKeyAsync against a real (in-memory) DbContext so the
/// global HasQueryFilter(!IsDeleted) on Notification is actually applied — a
/// mocked repo can't catch that the includeDeleted predicate is defeated by the
/// query filter unless IgnoreQueryFilters() is used.
/// </summary>
public class NotificationRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly NotificationRepository _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public NotificationRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _sut = new NotificationRepository(_context, Substitute.For<ILogger<NotificationRepository>>());
    }

    private async Task SeedAsync(string groupKey, bool isDeleted)
    {
        _context.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Type = NotificationType.BudgetThreshold,
            Title = "t",
            Body = "b",
            GroupKey = groupKey,
            IsDeleted = isDeleted
        });
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task ExistsByGroupKey_SoftDeleted_DefaultExcludes_IncludeDeletedFinds()
    {
        await SeedAsync("gk-period", isDeleted: true);

        // Default: the global !IsDeleted query filter hides it.
        (await _sut.ExistsByGroupKeyAsync(_userId, "gk-period")).Should().BeFalse();
        // includeDeleted: IgnoreQueryFilters() surfaces the soft-deleted row so a
        // period-deduplicated alert stays suppressed after the user deletes it.
        (await _sut.ExistsByGroupKeyAsync(_userId, "gk-period", includeDeleted: true)).Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByGroupKey_NotDeleted_FoundEitherWay()
    {
        await SeedAsync("gk-live", isDeleted: false);

        (await _sut.ExistsByGroupKeyAsync(_userId, "gk-live")).Should().BeTrue();
        (await _sut.ExistsByGroupKeyAsync(_userId, "gk-live", includeDeleted: true)).Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByGroupKey_DifferentUser_NotFound()
    {
        await SeedAsync("gk-live", isDeleted: false);

        (await _sut.ExistsByGroupKeyAsync(Guid.NewGuid(), "gk-live", includeDeleted: true)).Should().BeFalse();
    }

    public void Dispose() => _context.Dispose();
}
