using System.Collections.Concurrent;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Extensions.NpOn.ICommonDb.DbResults.Extensions;

namespace Common.Extensions.NpOn.HandleFlow;

public static class ResultConverter
{
    private static readonly ConcurrentDictionary<Type, Func<INpOnWrapperResult, IEnumerable<object>>> CachedListInvokers = new();

    /// <summary>
    /// Generic high-performance converter using Dynamic IL Emit.
    /// Does not require BaseCtrl - supports any class or record.
    /// </summary>
    public static IEnumerable<T>? GenericConverter<T>(this INpOnWrapperResult? result) where T : class, new()
    {
        if (result == null) return null;
        return result.ToList<T>();
    }

    /// <summary>
    /// Type-driven converter using cached compiled delegates and Dynamic IL Emit.
    /// Removes the hard requirement for BaseCtrl, supporting any class or record.
    /// </summary>
    public static IEnumerable<object>? GenericConverter(this INpOnWrapperResult? result, Type targetType)
    {
        if (result == null) return null;

        var invoker = CachedListInvokers.GetOrAdd(targetType, t =>
        {
            var method = typeof(NpOnWrapperResultExtensions)
                .GetMethod(nameof(NpOnWrapperResultExtensions.ToList), [typeof(INpOnWrapperResult)])!
                .MakeGenericMethod(t);

            return (r) =>
            {
                var res = method.Invoke(null, [r]);
                return (res as System.Collections.IEnumerable)?.Cast<object>() ?? [];
            };
        });

        return invoker(result);
    }
}
