using Microsoft.EntityFrameworkCore;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;
using MyMascada.Infrastructure.Repositories;

namespace MyMascada.Tests.Unit.Repositories;

public class UserDeviceRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly UserDeviceRepository _repository;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    public UserDeviceRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _repository = new UserDeviceRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task UpsertAsync_NewToken_CreatesDevice()
    {
        var device = await _repository.UpsertAsync(_userId, "token-1", "ios");

        device.UserId.Should().Be(_userId);
        device.FcmToken.Should().Be("token-1");
        device.Platform.Should().Be("ios");

        var stored = await _context.UserDevices.SingleAsync();
        stored.Id.Should().Be(device.Id);
    }

    [Fact]
    public async Task UpsertAsync_SameTokenTwice_IsIdempotent()
    {
        var first = await _repository.UpsertAsync(_userId, "token-1", "ios");
        var second = await _repository.UpsertAsync(_userId, "token-1", "ios");

        second.Id.Should().Be(first.Id);
        (await _context.UserDevices.CountAsync()).Should().Be(1);
        second.LastSeenAt.Should().BeOnOrAfter(first.LastSeenAt);
    }

    [Fact]
    public async Task UpsertAsync_TokenOwnedByAnotherUser_ReassignsOwnership()
    {
        await _repository.UpsertAsync(_otherUserId, "shared-device-token", "android");

        var device = await _repository.UpsertAsync(_userId, "shared-device-token", "android");

        device.UserId.Should().Be(_userId);
        (await _context.UserDevices.CountAsync()).Should().Be(1);
        (await _repository.GetByUserIdAsync(_otherUserId)).Should().BeEmpty();
    }

    [Fact]
    public async Task UpsertAsync_SoftDeletedToken_RevivesRow()
    {
        await _repository.UpsertAsync(_userId, "token-1", "ios");
        await _repository.DeleteByTokenAsync(_userId, "token-1");

        var revived = await _repository.UpsertAsync(_userId, "token-1", "ios");

        revived.IsDeleted.Should().BeFalse();
        revived.DeletedAt.Should().BeNull();
        (await _repository.GetByUserIdAsync(_userId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteByTokenAsync_OwnedToken_SoftDeletes()
    {
        await _repository.UpsertAsync(_userId, "token-1", "ios");

        await _repository.DeleteByTokenAsync(_userId, "token-1");

        (await _repository.GetByUserIdAsync(_userId)).Should().BeEmpty();
        var raw = await _context.UserDevices.IgnoreQueryFilters().SingleAsync();
        raw.IsDeleted.Should().BeTrue();
        raw.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteByTokenAsync_TokenOwnedByAnotherUser_IsNoOp()
    {
        await _repository.UpsertAsync(_otherUserId, "token-1", "ios");

        await _repository.DeleteByTokenAsync(_userId, "token-1");

        (await _repository.GetByUserIdAsync(_otherUserId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteByTokenAsync_UnknownToken_IsNoOp()
    {
        var act = () => _repository.DeleteByTokenAsync(_userId, "missing-token");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsOnlyOwnActiveDevices()
    {
        await _repository.UpsertAsync(_userId, "token-1", "ios");
        await _repository.UpsertAsync(_userId, "token-2", "android");
        await _repository.UpsertAsync(_otherUserId, "token-3", "ios");
        await _repository.DeleteByTokenAsync(_userId, "token-2");

        var devices = await _repository.GetByUserIdAsync(_userId);

        devices.Should().HaveCount(1);
        devices[0].FcmToken.Should().Be("token-1");
    }

    [Fact]
    public async Task RemoveTokensAsync_SoftDeletesMatchingTokensAcrossUsers()
    {
        await _repository.UpsertAsync(_userId, "stale-1", "ios");
        await _repository.UpsertAsync(_otherUserId, "stale-2", "android");
        await _repository.UpsertAsync(_userId, "healthy", "ios");

        await _repository.RemoveTokensAsync(new[] { "stale-1", "stale-2" });

        (await _repository.GetByUserIdAsync(_userId)).Select(d => d.FcmToken).Should().Equal("healthy");
        (await _repository.GetByUserIdAsync(_otherUserId)).Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveTokensAsync_EmptyCollection_IsNoOp()
    {
        await _repository.UpsertAsync(_userId, "token-1", "ios");

        await _repository.RemoveTokensAsync(Array.Empty<string>());

        (await _repository.GetByUserIdAsync(_userId)).Should().HaveCount(1);
    }
}
