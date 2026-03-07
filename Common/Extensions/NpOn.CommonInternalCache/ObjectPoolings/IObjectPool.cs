namespace Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;

public interface IObjectPool<T> where T : class // generic object pool.
{
    T Get(); // Retrieves an object from the pool.
    void Return(T obj); // Returns an object to the pool.
}