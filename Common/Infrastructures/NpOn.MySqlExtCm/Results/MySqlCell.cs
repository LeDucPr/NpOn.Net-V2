using System.Data;
using System.Reflection;
using System.Reflection.Emit;
using Common.Extensions.NpOn.CommonInternalCache.ObjectCachings;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using MySqlConnector;

namespace Common.Infrastructures.NpOn.MySqlExtCm.Results;

public class MySqlCell<T> : NpOnCell<T>
{
    private static readonly DbType CachedDbType = ResolveDbType(typeof(T));

    private MySqlCell(object? value, DbType dbType, string sourceTypeName, bool isPrimaryKey)
        : base(value, dbType, sourceTypeName, isPrimaryKey)
    {
    }

    private static DbType ResolveDbType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(Guid)) return DbType.Guid;
        if (underlying == typeof(byte[])) return DbType.Binary;
        if (underlying.IsEnum) return ResolveDbType(Enum.GetUnderlyingType(underlying));

        return Type.GetTypeCode(underlying) switch
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
            _ => DbType.Object
        };
    }

    /// Cell ( MySqlConnector -> DbType ) 
    public static MySqlCell<T> FromMySqlConnector(object? value, string sourceTypeName, bool isPrimaryKey)
    {
        return new MySqlCell<T>(value, CachedDbType, sourceTypeName, isPrimaryKey);
    }
}

public static class MySqlCellDynamicFactory
{
    private static readonly WrapperCacheStore<Type, Func<object?, string, bool, INpOnCell>> FactoryStore = new();

    public static INpOnCell Create(Type dotNetType, object? value, string sourceTypeName, bool isPrimaryKey)
    {
        var factory = FactoryStore.GetOrAdd(dotNetType, CreateFactory);
        return factory(value, sourceTypeName, isPrimaryKey);
    }

    private static Func<object?, string, bool, INpOnCell> CreateFactory(Type type)
    {
        // Create a DynamicMethod that matches the signature: INpOnCell Method(object? value, string sourceTypeName, bool isPrimaryKey)
        var dynamicMethod = new DynamicMethod(
            $"CreateMySqlCell_{type.Name}",
            typeof(INpOnCell), // Return type
            [typeof(object), typeof(string), typeof(bool)], // Parameter types: value, sourceTypeName, isPrimaryKey
            typeof(MySqlCellDynamicFactory).Module,
            true // Skip visibility checks to access private/internal members if needed
        );


        // Get the specific generic type: MySqlCell<T>
        var mysqlCellType = typeof(MySqlCell<>).MakeGenericType(type);

        // Get the static method: MySqlCell<T>.FromMySqlConnector(object?, string, bool)
        var fromMySqlConnectorMethod = mysqlCellType.GetMethod(
            nameof(MySqlCell<object>.FromMySqlConnector), // Name 
            BindingFlags.Public | BindingFlags.Static,
            null,
            [typeof(object), typeof(string), typeof(bool)],
            null
        );

        if (fromMySqlConnectorMethod == null)
            throw new InvalidOperationException($"Could not find method FromMySqlConnector on type {mysqlCellType.Name}");
        // IL Generation
        var il = dynamicMethod.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); // Load argument 0 (value: object)
        il.Emit(OpCodes.Ldarg_1); // Load argument 1 (sourceTypeName: string)
        il.Emit(OpCodes.Ldarg_2); // Load argument 2 (isPrimaryKey: bool)
        il.Emit(OpCodes.Call, fromMySqlConnectorMethod); // MySqlCell<T>.FromMySqlConnector

        // MySqlCell<T> - implements INpOnCell.
        il.Emit(OpCodes.Ret);
        return (Func<object?, string, bool, INpOnCell>)dynamicMethod.CreateDelegate(typeof(Func<object?, string, bool, INpOnCell>));
    }
}
