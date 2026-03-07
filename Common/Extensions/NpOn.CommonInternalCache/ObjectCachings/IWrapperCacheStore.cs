namespace Common.Extensions.NpOn.CommonInternalCache.ObjectCachings;

public interface IWrapperCacheStore<TKey, TValue> where TKey : notnull
{
    Dictionary<TKey, TValue> GetAll();
    TValue GetOrAdd(TKey key, Func<TKey, TValue> factory, TimeSpan? expiresIn = null);
    Task<TValue> GetOrAddAsync(TKey key, Func<TKey, Task<TValue>> factory, TimeSpan? expiresIn = null);
    TValue AddOrUpdate(TKey key, TValue value, TimeSpan? expiresIn = null);

    TValue AddOrUpdate(TKey key,
        Func<TKey, TValue> addFactory,
        Func<TKey, TValue, TValue> updateFactory,
        TimeSpan? expiresIn = null);

    bool TryGetValue(TKey key, out TValue? value);
    bool Remove(TKey key);
}