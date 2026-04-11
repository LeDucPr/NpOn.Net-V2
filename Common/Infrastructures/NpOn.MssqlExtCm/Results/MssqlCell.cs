using System.Data;
using System.Reflection;
using System.Reflection.Emit;
using Common.Extensions.NpOn.CommonInternalCache.ObjectCachings;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Microsoft.Data.SqlClient;

namespace Common.Infrastructures.NpOn.MssqlExtCm.Results;

public class MssqlCell<T> : NpOnCell<T>
{
    private MssqlCell(object? value, DbType dbType, string sourceTypeName, bool isPrimaryKey)
        : base(value, dbType, sourceTypeName, isPrimaryKey)
    {
    }

    public static MssqlCell<T> FromMssql(object? value, string sourceTypeName, bool isPrimaryKey)
    {
        var tempParam = new SqlParameter { Value = default(T) }; 
        var dbType = tempParam.DbType;
        return new MssqlCell<T>(value, dbType, sourceTypeName, isPrimaryKey);
    }
}

public static class MssqlCellDynamicFactory
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
            $"CreateMssqlCell_{type.Name}",
            typeof(INpOnCell),
            [typeof(object), typeof(string), typeof(bool)],
            typeof(MssqlCellDynamicFactory).Module,
            true
        );

        var mssqlCellType = typeof(MssqlCell<>).MakeGenericType(type);
        var fromMssqlMethod = mssqlCellType.GetMethod(
            nameof(MssqlCell<object>.FromMssql),
            BindingFlags.Public | BindingFlags.Static,
            null,
            [typeof(object), typeof(string), typeof(bool)],
            null
        );

        if (fromMssqlMethod == null)
            throw new InvalidOperationException($"Could not find method FromMssql on type {mssqlCellType.Name}");

        var il = dynamicMethod.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Call, fromMssqlMethod);
        il.Emit(OpCodes.Ret);

        return (Func<object?, string, bool, INpOnCell>)dynamicMethod.CreateDelegate(typeof(Func<object?, string, bool, INpOnCell>));
    }
}
