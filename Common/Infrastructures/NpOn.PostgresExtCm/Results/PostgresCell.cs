using System.Data;
using System.Reflection;
using System.Reflection.Emit;
using Common.Extensions.NpOn.CommonInternalCache.ObjectCachings;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Npgsql;

namespace Common.Infrastructures.NpOn.PostgresExtCm.Results;

public class PostgresCell<T> : NpOnCell<T>
{
    private PostgresCell(object? value, DbType dbType, string sourceTypeName)
        : base(value, dbType, sourceTypeName)
    {
    }

    /// Cell ( Npgsql -> DbType ) 
    public static PostgresCell<T> FromNpgsql(object? value, string sourceTypeName)
    {
        var tempParam = new NpgsqlParameter { Value = default(T) }; // Inference Npgsql (the best performance gen type)
        var dbType = tempParam.DbType;
        return new PostgresCell<T>(value, dbType, sourceTypeName);
    }
}

public static class PostgresCellDynamicFactory
{
    private static readonly WrapperCacheStore<Type, Func<object?, string, INpOnCell>> FactoryStore = new();

    public static INpOnCell Create(Type dotNetType, object? value, string sourceTypeName)
    {
        var factory = FactoryStore.GetOrAdd(dotNetType, CreateFactory);
        return factory(value, sourceTypeName);
    }

    private static Func<object?, string, INpOnCell> CreateFactory(Type type)
    {
        // Create a DynamicMethod that matches the signature: INpOnCell Method(object? value, string sourceTypeName)
        var dynamicMethod = new DynamicMethod(
            $"CreatePostgresCell_{type.Name}",
            typeof(INpOnCell), // Return type
            [typeof(object), typeof(string)], // Parameter types: value, sourceTypeName
            typeof(PostgresCellDynamicFactory).Module,
            true // Skip visibility checks to access private/internal members if needed
        );


        // Get the specific generic type: PostgresCell<T>
        var postgresCellType = typeof(PostgresCell<>).MakeGenericType(type);

        // Get the static method: PostgresCell<T>.FromNpgsql(object?, string)
        var fromNpgsqlMethod = postgresCellType.GetMethod(
            nameof(PostgresCell<object>.FromNpgsql), // Name 
            BindingFlags.Public | BindingFlags.Static,
            null,
            [typeof(object), typeof(string)],
            null
        );

        if (fromNpgsqlMethod == null)
            throw new InvalidOperationException($"Could not find method FromNpgsql on type {postgresCellType.Name}");
        // IL Generation
        var il = dynamicMethod.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); // Load argument 0 (value: object)
        il.Emit(OpCodes.Ldarg_1); // Load argument 1 (sourceTypeName: string)
        il.Emit(OpCodes.Call, fromNpgsqlMethod); // PostgresCell<T>.FromNpgsql

        // PostgresCell<T> - implements INpOnCell.
        il.Emit(OpCodes.Ret);
        return (Func<object?, string, INpOnCell>)dynamicMethod.CreateDelegate(typeof(Func<object?, string, INpOnCell>));
    }
}