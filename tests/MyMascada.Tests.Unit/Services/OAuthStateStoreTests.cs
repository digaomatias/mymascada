using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using MyMascada.Infrastructure.Services.Auth;

namespace MyMascada.Tests.Unit.Services;

public class OAuthStateStoreTests
{
    [Fact]
    public async Task ValidateAndConsumeAsync_WhenCalledConcurrently_OnlyOneRequestSucceeds()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var stateStore = new OAuthStateStore(memoryCache, NullLogger<OAuthStateStore>.Instance);
        var userId = Guid.NewGuid();
        const string state = "state-123";

        await stateStore.StoreAsync(userId, state);

        var validationTasks = Enumerable.Range(0, 20)
            .Select(_ => stateStore.ValidateAndConsumeAsync(userId, state))
            .ToArray();

        var results = await Task.WhenAll(validationTasks);

        results.Count(result => result).Should().Be(1);
    }

    [Fact]
    public async Task StoreAsync_WhileValidationIsInProgress_PreservesNewState()
    {
        using var memoryCache = new BlockingMemoryCache();
        var stateStore = new OAuthStateStore(memoryCache, NullLogger<OAuthStateStore>.Instance);
        var userId = Guid.NewGuid();

        await stateStore.StoreAsync(userId, "state-1");

        memoryCache.BlockNextRemove();
        var firstValidationTask = Task.Run(() => stateStore.ValidateAndConsumeAsync(userId, "state-1"));
        memoryCache.WaitUntilRemoveStarts();

        var concurrentStoreTask = Task.Run(() => stateStore.StoreAsync(userId, "state-2"));
        memoryCache.AllowRemoveToContinue();

        (await firstValidationTask).Should().BeTrue();
        await concurrentStoreTask;

        var secondValidationResult = await stateStore.ValidateAndConsumeAsync(userId, "state-2");
        secondValidationResult.Should().BeTrue();
    }

    private sealed class BlockingMemoryCache : IMemoryCache
    {
        private readonly IMemoryCache _innerCache = new MemoryCache(new MemoryCacheOptions());
        private readonly ManualResetEventSlim _removeStarted = new(false);
        private readonly ManualResetEventSlim _allowRemove = new(false);
        private volatile bool _blockNextRemove;

        public ICacheEntry CreateEntry(object key) => _innerCache.CreateEntry(key);

        public void Remove(object key)
        {
            if (_blockNextRemove)
            {
                _removeStarted.Set();
                _allowRemove.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
                _blockNextRemove = false;
            }

            _innerCache.Remove(key);
        }

        public bool TryGetValue(object key, out object? value) => _innerCache.TryGetValue(key, out value);

        public void BlockNextRemove()
        {
            _removeStarted.Reset();
            _allowRemove.Reset();
            _blockNextRemove = true;
        }

        public void WaitUntilRemoveStarts()
        {
            _removeStarted.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
        }

        public void AllowRemoveToContinue() => _allowRemove.Set();

        public void Dispose()
        {
            _innerCache.Dispose();
            _removeStarted.Dispose();
            _allowRemove.Dispose();
        }
    }
}
