using System.Collections.Concurrent;

namespace Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;

public class ObjectPool<T> : IObjectPool<T> where T : class // A thread-safe object pool implementation.
{
    private readonly ConcurrentBag<T> _objects;
    private readonly Func<T> _objectFactory;

    // Initializes a new instance of the <see cref="ObjectPool{T}"/> class.
    public ObjectPool(Func<T> objectFactory)
    {
        _objectFactory = objectFactory ?? throw new ArgumentNullException(nameof(objectFactory));
        _objects = new ConcurrentBag<T>();
    }

    // Retrieves an object from the pool or creates a new one if the pool is empty.
    public T Get() => _objects.TryTake(out T? item) ? item : _objectFactory();


    public void Return(T item) // Returns an object to the pool for future reuse.
        => _objects.Add(item); // add logic here to reset the object's state before returning it to the pool.
}