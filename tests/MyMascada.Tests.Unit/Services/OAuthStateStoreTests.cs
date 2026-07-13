using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyMascada.Domain.Common;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;
using MyMascada.Infrastructure.Services.Auth;

namespace MyMascada.Tests.Unit.Services;

public class OAuthStateStoreTests
{
    // Each store gets a fresh DbContext over the same backing database, so "a new
    // store" models a new request — or, crucially, a restarted/idled-down machine.
    private static DbContextOptions<ApplicationDbContext> NewDatabase() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

    private static OAuthStateStore StoreFor(DbContextOptions<ApplicationDbContext> options) =>
        new(new ApplicationDbContext(options), NullLogger<OAuthStateStore>.Instance);

    [Fact]
    public async Task ValidateAndConsumeAsync_WithMatchingState_Succeeds()
    {
        var options = NewDatabase();
        var userId = Guid.NewGuid();

        await StoreFor(options).StoreAsync(userId, "state-abc");
        var result = await StoreFor(options).ValidateAndConsumeAsync(userId, "state-abc");

        result.Should().BeTrue();
    }

    /// <summary>
    /// The regression this store exists for. On Fly the API scales to zero, so the
    /// machine can idle down or restart while the user is away authenticating with
    /// their bank. State held in process memory would be gone by the time the
    /// callback lands, rejecting a valid consent. A fresh context stands in for that
    /// new process — nothing survives except what was persisted.
    /// </summary>
    [Fact]
    public async Task ValidateAndConsumeAsync_AfterProcessRestart_StillSucceeds()
    {
        var options = NewDatabase();
        var userId = Guid.NewGuid();

        await StoreFor(options).StoreAsync(userId, "survives-restart");

        var afterRestart = StoreFor(options);
        var result = await afterRestart.ValidateAndConsumeAsync(userId, "survives-restart");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_IsSingleUse()
    {
        var options = NewDatabase();
        var userId = Guid.NewGuid();

        await StoreFor(options).StoreAsync(userId, "one-shot");

        var first = await StoreFor(options).ValidateAndConsumeAsync(userId, "one-shot");
        var second = await StoreFor(options).ValidateAndConsumeAsync(userId, "one-shot");

        first.Should().BeTrue();
        second.Should().BeFalse("the state is consumed on first use");
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WithMismatchedState_FailsAndBurnsTheStoredState()
    {
        var options = NewDatabase();
        var userId = Guid.NewGuid();

        await StoreFor(options).StoreAsync(userId, "real-state");

        var wrongGuess = await StoreFor(options).ValidateAndConsumeAsync(userId, "wrong-guess");
        wrongGuess.Should().BeFalse();

        // The stored state must be burned even though the guess was wrong, otherwise
        // an attacker could keep guessing against the same live state.
        var correctAfterWrongGuess = await StoreFor(options).ValidateAndConsumeAsync(userId, "real-state");
        correctAfterWrongGuess.Should().BeFalse("a failed attempt must still consume the state");
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WhenExpired_Fails()
    {
        var options = NewDatabase();
        var userId = Guid.NewGuid();

        await using (var seed = new ApplicationDbContext(options))
        {
            seed.OAuthStates.Add(new OAuthState
            {
                UserId = userId,
                State = "stale",
                ExpiresAt = DateTimeProvider.UtcNow.AddMinutes(-1),
                CreatedAt = DateTimeProvider.UtcNow.AddMinutes(-11)
            });
            await seed.SaveChangesAsync();
        }

        var result = await StoreFor(options).ValidateAndConsumeAsync(userId, "stale");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAndConsumeAsync_WithAnotherUsersState_Fails()
    {
        var options = NewDatabase();
        var victimId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();

        await StoreFor(options).StoreAsync(victimId, "victim-state");

        var result = await StoreFor(options).ValidateAndConsumeAsync(attackerId, "victim-state");

        result.Should().BeFalse("state is bound to the user it was issued to");
    }

    [Fact]
    public async Task StoreAsync_CalledTwice_InvalidatesTheAbandonedState()
    {
        var options = NewDatabase();
        var userId = Guid.NewGuid();

        await StoreFor(options).StoreAsync(userId, "first-attempt");
        await StoreFor(options).StoreAsync(userId, "second-attempt");

        var abandoned = await StoreFor(options).ValidateAndConsumeAsync(userId, "first-attempt");
        abandoned.Should().BeFalse("re-initiating replaces the earlier attempt's state");
    }

    [Fact]
    public async Task StoreAsync_CalledTwice_KeepsTheNewestStateValid()
    {
        var options = NewDatabase();
        var userId = Guid.NewGuid();

        await StoreFor(options).StoreAsync(userId, "first-attempt");
        await StoreFor(options).StoreAsync(userId, "second-attempt");

        var current = await StoreFor(options).ValidateAndConsumeAsync(userId, "second-attempt");
        current.Should().BeTrue();
    }

    [Fact]
    public async Task StoreAsync_SweepsExpiredStatesOfOtherUsers()
    {
        var options = NewDatabase();
        var expiredUser = Guid.NewGuid();

        await using (var seed = new ApplicationDbContext(options))
        {
            seed.OAuthStates.Add(new OAuthState
            {
                UserId = expiredUser,
                State = "expired",
                ExpiresAt = DateTimeProvider.UtcNow.AddMinutes(-5),
                CreatedAt = DateTimeProvider.UtcNow.AddMinutes(-15)
            });
            await seed.SaveChangesAsync();
        }

        await StoreFor(options).StoreAsync(Guid.NewGuid(), "fresh");

        await using var verify = new ApplicationDbContext(options);
        var stillThere = await verify.OAuthStates.AnyAsync(s => s.UserId == expiredUser);
        stillThere.Should().BeFalse("expired states are swept when a new one is stored");
    }
}
