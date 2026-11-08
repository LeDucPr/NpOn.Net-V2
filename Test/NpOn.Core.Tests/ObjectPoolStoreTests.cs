using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Xunit;

namespace NpOn.Core.Tests;

public class ObjectPoolStoreTests
{
    [Fact]
    public void ObjectPool_GetAndReturn_ShouldRecycleInstances()
    {
        // Arrange
        var pool = new ObjectPool<NpOnWrapperResult>(() => new NpOnWrapperResult());

        // Act
        var item1 = pool.Get();
        Assert.NotNull(item1);

        pool.Return(item1);

        var item2 = pool.Get();

        // Assert - recycled instance should be the same reference
        Assert.Same(item1, item2);
    }

    [Fact]
    public void ObjectPoolStore_PreAllocate_ShouldInitializePool()
    {
        // Arrange
        IObjectPoolStore store = new ObjectPoolStore();

        // Act
        store.PreAllocate(() => new NpOnWrapperResult(), 50);
        var pool = store.GetPool(() => new NpOnWrapperResult());

        // Assert
        Assert.NotNull(pool);
        var item = pool.Get();
        Assert.NotNull(item);
        pool.Return(item);
    }

    [Fact]
    public void ObjectPool_MultithreadedAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var pool = new ObjectPool<List<int>>(() => new List<int>());
        const int threadCount = 20;
        const int iterationsPerThread = 500;
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        // Act
        Parallel.For(0, threadCount, _ =>
        {
            try
            {
                for (int i = 0; i < iterationsPerThread; i++)
                {
                    var list = pool.Get();
                    list.Add(i);
                    list.Clear();
                    pool.Return(list);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        // Assert
        Assert.Empty(exceptions);
    }
}
