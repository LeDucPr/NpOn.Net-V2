using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Extensions.NpOn.CommonInternalCache;

/// <summary>
/// A thread-safe, in-memory cache store that provides protection against cache stampede (thundering herd) issues.
/// </summary>
public class WrapperCacheStore<TKey, TValue> : IWrapperCacheStore<TKey, TValue> where TKey : notnull
{
    // We store AsyncLazy<WrapperCache<TValue>> to ensure the factory is only executed once per key
    // even with concurrent requests.
    private readonly ConcurrentDictionary<TKey, Lazy<Task<WrapperCache<TValue>>>> _dict = new();

    public Dictionary<TKey, TValue> GetAll()
    {
        var result = new Dictionary<TKey, TValue>();
        foreach (var pair in _dict)
        {
            // Only include completed and not-expired tasks
            if (pair.Value.IsValueCreated && 
                pair.Value.Value.IsCompletedSuccessfully && 
                !pair.Value.Value.Result.IsExpired)
            {
                result[pair.Key] = pair.Value.Value.Result.Value;
            }
        }
        return result;
    }

    public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory, TimeSpan? expiresIn = null)
    {
        // This is a simplified version for sync-over-async. For high-contention sync scenarios,
        // a different locking mechanism might be better, but this maintains consistency with the async approach.
        return GetOrAddAsync(key, k => Task.FromResult(factory(k)), expiresIn).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Asynchronously gets a value from the cache or adds it if it doesn't exist.
    /// This method is protected against cache stampede.
    /// </summary>
    public async Task<TValue> GetOrAddAsync(
        TKey key,
        Func<TKey, Task<TValue>> factory,
        TimeSpan? expiresIn = null)
    {
        while (true)
        {
            if (_dict.TryGetValue(key, out var lazyWrapperTask))
            {
                var wrapper = await lazyWrapperTask.Value;
                if (!wrapper.IsExpired)
                {
                    return wrapper.Value;
                }
                
                // The entry has expired. We try to remove it.
                // If we succeed, the loop will continue and we'll create a new factory.
                // If we fail, it means another thread just removed it, so the loop will continue
                // and we'll likely get the new value in the next TryGetValue.
                var pair = new KeyValuePair<TKey, Lazy<Task<WrapperCache<TValue>>>>(key, lazyWrapperTask);
                _dict.TryRemove(pair); 
                continue; // Retry the loop
            }

            // The key doesn't exist. Create a new lazy factory.
            var newLazyWrapperTask = new Lazy<Task<WrapperCache<TValue>>>(async () =>
            {
                var newValue = await factory(key);
                return new WrapperCache<TValue>(newValue, expiresIn);
            });

            // Try to add the new lazy factory.
            // If another thread added it in the meantime, GetOrAdd returns the existing one.
            var existingOrNewLazy = _dict.GetOrAdd(key, newLazyWrapperTask);
            
            // Whether we added a new one or got an existing one, we await its value.
            // The Lazy<> ensures the factory is only ever executed once.
            var finalWrapper = await existingOrNewLazy.Value;

            // It's possible the value we just got/created has already expired (e.g., very short TTL).
            // If so, we loop again. Otherwise, we're done.
            if (!finalWrapper.IsExpired)
            {
                return finalWrapper.Value;
            }
        }
    }

    public TValue AddOrUpdate(TKey key, TValue value, TimeSpan? expiresIn = null)
    {
        var wrapper = new WrapperCache<TValue>(value, expiresIn);
        var lazy = new Lazy<Task<WrapperCache<TValue>>>(() => Task.FromResult(wrapper));
        _dict.AddOrUpdate(key, lazy, (_, _) => lazy);
        return value;
    }

    public TValue AddOrUpdate(
        TKey key,
        Func<TKey, TValue> addFactory,
        Func<TKey, TValue, TValue> updateFactory,
        TimeSpan? expiresIn = null)
    {
        var newLazy = new Lazy<Task<WrapperCache<TValue>>>(() => 
            Task.FromResult(new WrapperCache<TValue>(addFactory(key), expiresIn)));

        var resultLazy = _dict.AddOrUpdate(key, newLazy, (k, existingLazy) =>
        {
            // This update logic is complex in a stampede-proof scenario.
            // For simplicity, we'll just replace the old task with a new one.
            // A more robust implementation might await the old task first.
            var updatedValue = updateFactory(k, existingLazy.Value.GetAwaiter().GetResult().Value);
            return new Lazy<Task<WrapperCache<TValue>>>(() => 
                Task.FromResult(new WrapperCache<TValue>(updatedValue, expiresIn)));
        });

        return resultLazy.Value.GetAwaiter().GetResult().Value;
    }

    public bool TryGetValue(TKey key, out TValue? value)
    {
        value = default;
        if (_dict.TryGetValue(key, out var lazyWrapperTask))
        {
            // Check if the task has completed successfully and the result is not expired.
            if (lazyWrapperTask.IsValueCreated && 
                lazyWrapperTask.Value.IsCompletedSuccessfully && 
                !lazyWrapperTask.Value.Result.IsExpired)
            {
                value = lazyWrapperTask.Value.Result.Value;
                return true;
            }
        }
        return false;
    }

    public bool Remove(TKey key) => _dict.TryRemove(key, out _);
}
