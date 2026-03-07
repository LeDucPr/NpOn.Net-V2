using System.Collections.Concurrent;

namespace Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;

// A thread-safe store for managing and accessing multiple object pools.
public class ObjectPoolStore : IObjectPoolStore
{
    private readonly ConcurrentDictionary<Type, object> _pools = new();

    // Gets or creates an object pool for the specified type.
    public IObjectPool<T> GetPool<T>(Func<T> objectFactory) where T : class =>
        (IObjectPool<T>)_pools.GetOrAdd(typeof(T), _ => new ObjectPool<T>(objectFactory));

    // Pre-populates a pool with a specified number of objects.
    public ObjectPoolStore PreAllocate<T>(Func<T> objectFactory, int initialSize) where T : class
    {
        if (initialSize <= 0)
            return this;
        var pool = GetPool(objectFactory);
        for (int i = 0; i < initialSize; i++)
        {
            pool.Return(objectFactory());
        }
        return this;
    }
}