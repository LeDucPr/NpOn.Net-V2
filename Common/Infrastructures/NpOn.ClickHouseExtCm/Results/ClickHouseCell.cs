using System.Data;
using System.Reflection;
using System.Reflection.Emit;
using Common.Extensions.NpOn.CommonDb.Results;
using Common.Extensions.NpOn.CommonInternalCache.ObjectCachings;
using Common.Extensions.NpOn.ICommonDb.DbResults;

namespace Common.Infrastructures.NpOn.ClickHouseExtCm.Results;

public class ClickHouseCell<T> : NpOnCell<T>
{
    private ClickHouseCell(object? value, DbType dbType, string sourceTypeName, bool isPrimaryKey)
        : base(value, dbType, sourceTypeName, isPrimaryKey)
    {
    }

    public static ClickHouseCell<T> FromClickHouse(object? value, string sourceTypeName, bool isPrimaryKey)
    {
        var dbType = ClickHouseUtils.GetDbType(typeof(T));
        return new ClickHouseCell<T>(value, dbType, sourceTypeName, isPrimaryKey);
    }
}

public static class ClickHouseCellDynamicFactory
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
            $"CreateClickHouseCell_{type.Name}",
            typeof(INpOnCell),
            new[] { typeof(object), typeof(string), typeof(bool) },
            typeof(ClickHouseCellDynamicFactory).Module,
            true
        );

        var clickHouseCellType = typeof(ClickHouseCell<>).MakeGenericType(type);

        var fromClickHouseMethod = clickHouseCellType.GetMethod(
            nameof(ClickHouseCell<object>.FromClickHouse),
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(object), typeof(string), typeof(bool) },
            null
        );

        if (fromClickHouseMethod == null)
            throw new InvalidOperationException($"Could not find method FromClickHouse on type {clickHouseCellType.Name}");

        var il = dynamicMethod.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); // value
        il.Emit(OpCodes.Ldarg_1); // sourceTypeName
        il.Emit(OpCodes.Ldarg_2); // isPrimaryKey
        il.Emit(OpCodes.Call, fromClickHouseMethod); 
        il.Emit(OpCodes.Ret);

        return (Func<object?, string, bool, INpOnCell>)dynamicMethod.CreateDelegate(typeof(Func<object?, string, bool, INpOnCell>));
    }
}
