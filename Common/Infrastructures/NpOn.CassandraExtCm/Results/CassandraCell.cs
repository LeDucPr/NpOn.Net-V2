using System.Data;
using System.Reflection;
using System.Reflection.Emit;
using Common.Extensions.NpOn.CommonInternalCache.ObjectCachings;
using Common.Extensions.NpOn.ICommonDb.DbResults;

namespace Common.Infrastructures.NpOn.CassandraExtCm.Results;

public class CassandraCell<T> : NpOnCell<T>
{
    private CassandraCell(object? value, DbType dbType, string sourceTypeName, bool isPrimaryKey)
        : base(value, dbType, sourceTypeName, isPrimaryKey)
    {
    }

    /// <summary>
    /// Cell (Cassandra -> DbType)
    /// </summary>
    public static CassandraCell<T> FromCassandra(object? value, string sourceTypeName, bool isPrimaryKey)
    {
        var dbType = CassandraUtils.GetDbType(typeof(T));
        return new CassandraCell<T>(value, dbType, sourceTypeName, isPrimaryKey);
    }
}

public static class CassandraCellDynamicFactory
{
    private static readonly WrapperCacheStore<Type, Func<object?, string, bool, INpOnCell>> FactoryStore = new();

    public static INpOnCell Create(Type dotNetType, object? value, string sourceTypeName, bool isPrimaryKey)
    {
        var factory = FactoryStore.GetOrAdd(dotNetType, CreateFactory);
        return factory(value, sourceTypeName, isPrimaryKey);
    }

    private static Func<object?, string, bool, INpOnCell> CreateFactory(Type type)
    {
        var dynamicMethod = new DynamicMethod(
            $"CreateCassandraCell_{type.Name}",
            typeof(INpOnCell),
            new[] { typeof(object), typeof(string), typeof(bool) },
            typeof(CassandraCellDynamicFactory).Module,
            true
        );

        var cassandraCellType = typeof(CassandraCell<>).MakeGenericType(type);

        var fromCassandraMethod = cassandraCellType.GetMethod(
            nameof(CassandraCell<object>.FromCassandra),
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(object), typeof(string), typeof(bool) }, // overload parameters
            null
        );

        if (fromCassandraMethod == null)
            throw new InvalidOperationException($"Could not find method FromCassandra on type {cassandraCellType.Name}");

        var il = dynamicMethod.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); // value
        il.Emit(OpCodes.Ldarg_1); // sourceTypeName
        il.Emit(OpCodes.Ldarg_2); // isPrimaryKey
        il.Emit(OpCodes.Call, fromCassandraMethod); 
        il.Emit(OpCodes.Ret);

        return (Func<object?, string, bool, INpOnCell>)dynamicMethod.CreateDelegate(typeof(Func<object?, string, bool, INpOnCell>));
    }
}
