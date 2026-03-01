using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using Common.Extensions.NpOn.CommonInternalCache;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Npgsql;

namespace Common.Infrastructures.NpOn.PostgresExtCm.Results;

public class PostgresCell<T> : NpOnCell<T>
{
    public PostgresCell(object? value, DbType dbType, string sourceTypeName)
        : base(value, dbType, sourceTypeName)
    {
    }

    /// Cell ( Npgsql -> DbType ) 
    public static PostgresCell<T> FromNpgsql(object? value, string sourceTypeName)
    {
        // Inference Npgsql (the best performance)
        var tempParam = new NpgsqlParameter { Value = default(T) };
        var dbType = tempParam.DbType;
        return new PostgresCell<T>(value, dbType, sourceTypeName);
    }
}

public static class PostgresCellDynamicFactory
{
    private static readonly WrapperCacheStore<Type, Func<object?, string, INpOnCell>> FactoryStore = new();
    public static INpOnCell Create(Type dotNetType, object? value, string sourceTypeName)
    {
        var factory = FactoryStore.GetOrAdd(dotNetType, t =>
        {
            var valParam = Expression.Parameter(typeof(object), "npg_value");
            var nameParam = Expression.Parameter(typeof(string), "npg_name");

            // Gọi phương thức static FromNpgsql của PostgresCell<T>
            var method = typeof(PostgresCell<>)
                .MakeGenericType(t)
                .GetMethod(nameof(PostgresCell<>.FromNpgsql), BindingFlags.Public | BindingFlags.Static);
            var call = Expression.Call(method!, valParam, nameParam);
            return Expression.Lambda<Func<object?, string, INpOnCell>>(call, valParam, nameParam).Compile();
        });

        return factory(value, sourceTypeName);
    }
}