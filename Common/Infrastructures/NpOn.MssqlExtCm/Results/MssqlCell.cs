using System.Data;
using System.Reflection;
using System.Reflection.Emit;
using Common.Extensions.NpOn.CommonInternalCache.ObjectCachings;
using Common.Extensions.NpOn.ICommonDb.DbResults;

namespace Common.Infrastructures.NpOn.MssqlExtCm.Results;

public class MssqlCell<T> : NpOnCell<T>
{
    public static readonly DbType CachedDbType = ResolveDbType(typeof(T));

    public static DbType ResolveDbType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsEnum) type = Enum.GetUnderlyingType(type);

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => DbType.Boolean,
            TypeCode.Byte => DbType.Byte,
            TypeCode.SByte => DbType.SByte,
            TypeCode.Int16 => DbType.Int16,
            TypeCode.UInt16 => DbType.UInt16,
            TypeCode.Int32 => DbType.Int32,
            TypeCode.UInt32 => DbType.UInt32,
            TypeCode.Int64 => DbType.Int64,
            TypeCode.UInt64 => DbType.UInt64,
            TypeCode.Single => DbType.Single,
            TypeCode.Double => DbType.Double,
            TypeCode.Decimal => DbType.Decimal,
            TypeCode.DateTime => DbType.DateTime,
            TypeCode.String => DbType.String,
            _ => type switch
            {
                _ when type == typeof(Guid) => DbType.Guid,
                _ when type == typeof(byte[]) => DbType.Binary,
                _ when type == typeof(DateTimeOffset) => DbType.DateTimeOffset,
                _ when type == typeof(TimeSpan) => DbType.Time,
                _ => DbType.Object
            }
        };
    }

    public MssqlCell(object? value, string sourceTypeName, bool isPrimaryKey)
        : base(value, CachedDbType, sourceTypeName, isPrimaryKey)
    {
    }

    public static MssqlCell<T> FromMssql(object? value, string sourceTypeName, bool isPrimaryKey)
    {
        return new MssqlCell<T>(value, sourceTypeName, isPrimaryKey);
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
