namespace Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;

public interface IObjectPoolStore // Defines the interface for a store that manages multiple object pools.
{
    // Gets the object pool for a specific type.
    // If the pool does not exist, it will be created using the provided factory.
    IObjectPool<T> GetPool<T>(Func<T> objectFactory) where T : class;

    // Pre-allocates a specified number of objects for a given type's pool.
    ObjectPoolStore PreAllocate<T>(Func<T> objectFactory, int initialSize) where T : class;
}