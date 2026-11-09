using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using System.Reflection.Emit;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Npgsql;

namespace Common.Infrastructures.NpOn.PostgresExtCm.Results;

public class PostgresCell<T> : NpOnCell<T>
{
    // Thread-safe static initialization: Computed ONCE per generic type T by the CLR.
    // Eliminates millions of NpgsqlParameter allocations on hot-path.
    private static readonly DbType CachedDbType;

    static PostgresCell()
    {
        CachedDbType = ResolveDbType(typeof(T));
    }

    private static DbType ResolveDbType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying.IsEnum)
            return ResolveDbType(Enum.GetUnderlyingType(underlying));

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
            TypeCode.Char => DbType.StringFixedLength,
            _ => underlying switch
            {
                _ when underlying == typeof(Guid) => DbType.Guid,
                _ when underlying == typeof(DateTimeOffset) => DbType.DateTimeOffset,
                _ when underlying == typeof(TimeSpan) => DbType.Time,
                _ when underlying == typeof(byte[]) => DbType.Binary,
                _ => DbType.Object
            }
        };
    }

    public PostgresCell(object? value, DbType dbType, string sourceTypeName, bool isPrimaryKey)
        : base(value, dbType, sourceTypeName, isPrimaryKey)
    {
    }

    /// Cell ( Npgsql -> DbType ) 
    public static PostgresCell<T> FromNpgsql(object? value, string sourceTypeName, bool isPrimaryKey)
    {
        return new PostgresCell<T>(value, CachedDbType, sourceTypeName, isPrimaryKey);
    }
}

public static class PostgresCellDynamicFactory
{
    // Fast lock-free ConcurrentDictionary for multi-threaded delegate dispatch
    private static readonly ConcurrentDictionary<Type, Func<object?, string, bool, INpOnCell>> FactoryStore = new();

    public static INpOnCell Create(Type dotNetType, object? value, string sourceTypeName, bool isPrimaryKey)
    {
        var factory = FactoryStore.GetOrAdd(dotNetType, CreateFactory);
        return factory(value, sourceTypeName, isPrimaryKey);
    }

    private static Func<object?, string, bool, INpOnCell> CreateFactory(Type type)
    {
        // Create a DynamicMethod that matches the signature: INpOnCell Method(object? value, string sourceTypeName, bool isPrimaryKey)
        var dynamicMethod = new DynamicMethod(
            $"CreatePostgresCell_{type.Name}",
            typeof(INpOnCell), // Return type
            [typeof(object), typeof(string), typeof(bool)], // Parameter types: value, sourceTypeName, isPrimaryKey
            typeof(PostgresCellDynamicFactory).Module,
            true // Skip visibility checks to access private/internal members if needed
        );

        // Get the specific generic type: PostgresCell<T>
        var postgresCellType = typeof(PostgresCell<>).MakeGenericType(type);

        // Get the static method: PostgresCell<T>.FromNpgsql(object?, string, bool)
        var fromNpgsqlMethod = postgresCellType.GetMethod(
            nameof(PostgresCell<object>.FromNpgsql), // Name 
            BindingFlags.Public | BindingFlags.Static,
            null,
            [typeof(object), typeof(string), typeof(bool)],
            null
        );

        if (fromNpgsqlMethod == null)
            throw new InvalidOperationException($"Could not find method FromNpgsql on type {postgresCellType.Name}");

        // IL Generation
        var il = dynamicMethod.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); // Load argument 0 (value: object)
        il.Emit(OpCodes.Ldarg_1); // Load argument 1 (sourceTypeName: string)
        il.Emit(OpCodes.Ldarg_2); // Load argument 2 (isPrimaryKey: bool)
        il.Emit(OpCodes.Call, fromNpgsqlMethod); // PostgresCell<T>.FromNpgsql

        // PostgresCell<T> - implements INpOnCell.
        il.Emit(OpCodes.Ret);
        return (Func<object?, string, bool, INpOnCell>)dynamicMethod.CreateDelegate(typeof(Func<object?, string, bool, INpOnCell>));
    }
}
