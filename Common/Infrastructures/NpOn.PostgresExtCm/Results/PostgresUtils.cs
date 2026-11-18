using System.Collections.Concurrent;
using System.ComponentModel;
using System.Data;
using System.Text.Json;
using Common.Extensions.NpOn.CommonMode;
using Npgsql;
using NpgsqlTypes;

namespace Common.Infrastructures.NpOn.PostgresExtCm.Results;

public static class PostgresUtils
{
    // Thread-safe type to NpgsqlDbType cache
    private static readonly ConcurrentDictionary<Type, NpgsqlDbType> TypeToNpgsqlDbTypeCache = new();

    // Thread-safe NpgsqlDbType to DbType cache
    private static readonly ConcurrentDictionary<NpgsqlDbType, DbType> NpgsqlDbTypeToDbTypeCache = new();

    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    static PostgresUtils()
    {
        // Pre-populate fast primitive mappings (Zero allocation lookup)
        TypeToNpgsqlDbTypeCache[typeof(int)] = NpgsqlDbType.Integer;
        TypeToNpgsqlDbTypeCache[typeof(long)] = NpgsqlDbType.Bigint;
        TypeToNpgsqlDbTypeCache[typeof(short)] = NpgsqlDbType.Smallint;
        TypeToNpgsqlDbTypeCache[typeof(byte)] = NpgsqlDbType.Smallint;
        TypeToNpgsqlDbTypeCache[typeof(sbyte)] = NpgsqlDbType.Smallint;
        TypeToNpgsqlDbTypeCache[typeof(uint)] = NpgsqlDbType.Bigint;
        TypeToNpgsqlDbTypeCache[typeof(ulong)] = NpgsqlDbType.Numeric;
        TypeToNpgsqlDbTypeCache[typeof(ushort)] = NpgsqlDbType.Integer;
        TypeToNpgsqlDbTypeCache[typeof(string)] = NpgsqlDbType.Text;
        TypeToNpgsqlDbTypeCache[typeof(bool)] = NpgsqlDbType.Boolean;
        TypeToNpgsqlDbTypeCache[typeof(Guid)] = NpgsqlDbType.Uuid;
        TypeToNpgsqlDbTypeCache[typeof(float)] = NpgsqlDbType.Real;
        TypeToNpgsqlDbTypeCache[typeof(double)] = NpgsqlDbType.Double;
        TypeToNpgsqlDbTypeCache[typeof(decimal)] = NpgsqlDbType.Numeric;
        TypeToNpgsqlDbTypeCache[typeof(byte[])] = NpgsqlDbType.Bytea;
        TypeToNpgsqlDbTypeCache[typeof(char)] = NpgsqlDbType.Char;
        TypeToNpgsqlDbTypeCache[typeof(TimeSpan)] = NpgsqlDbType.Interval;
        TypeToNpgsqlDbTypeCache[typeof(DateTime)] = NpgsqlDbType.TimestampTz;
        TypeToNpgsqlDbTypeCache[typeof(DateTimeOffset)] = NpgsqlDbType.TimestampTz;
        TypeToNpgsqlDbTypeCache[typeof(DateOnly)] = NpgsqlDbType.Date;
        TypeToNpgsqlDbTypeCache[typeof(TimeOnly)] = NpgsqlDbType.Time;
        TypeToNpgsqlDbTypeCache[typeof(string[])] = NpgsqlDbType.Array | NpgsqlDbType.Text;
        TypeToNpgsqlDbTypeCache[typeof(int[])] = NpgsqlDbType.Array | NpgsqlDbType.Integer;
        TypeToNpgsqlDbTypeCache[typeof(long[])] = NpgsqlDbType.Array | NpgsqlDbType.Bigint;
        TypeToNpgsqlDbTypeCache[typeof(Guid[])] = NpgsqlDbType.Array | NpgsqlDbType.Uuid;
        TypeToNpgsqlDbTypeCache[typeof(bool[])] = NpgsqlDbType.Array | NpgsqlDbType.Boolean;
        TypeToNpgsqlDbTypeCache[typeof(double[])] = NpgsqlDbType.Array | NpgsqlDbType.Double;
        TypeToNpgsqlDbTypeCache[typeof(decimal[])] = NpgsqlDbType.Array | NpgsqlDbType.Numeric;
    }

    public static object? NormalizePostgresValue(this object? value)
    {
        return value;
    }

    public static (object? Value, NpgsqlDbType DbType) NormalizeForNpgsql(object? raw)
    {
        if (raw == null || raw == DBNull.Value)
            return (DBNull.Value, NpgsqlDbType.Unknown);

        if (raw is DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Unspecified)
                return (DateTime.SpecifyKind(dt, DateTimeKind.Utc), NpgsqlDbType.TimestampTz);
            if (dt.Kind == DateTimeKind.Local)
                return (dt.ToUniversalTime(), NpgsqlDbType.TimestampTz);
            return (dt, NpgsqlDbType.TimestampTz);
        }

        if (raw is DateTimeOffset dto)
            return (dto, NpgsqlDbType.TimestampTz);

        if (raw is DateOnly dOnly)
            return (dOnly, NpgsqlDbType.Date);

        if (raw is TimeOnly tOnly)
            return (tOnly, NpgsqlDbType.Time);

        var type = raw.GetType();

        // Arrays handling
        if (type.IsArray)
        {
            if (TypeToNpgsqlDbTypeCache.TryGetValue(type, out var arrayDbType))
                return (raw, arrayDbType);

            var elemType = type.GetElementType()!;
            if (elemType == typeof(byte))
                return (raw, NpgsqlDbType.Bytea);

            return (raw, NpgsqlDbType.Array);
        }

        // Fast lookup for cached / primitive types
        if (TypeToNpgsqlDbTypeCache.TryGetValue(type, out var npgsqlDbType))
        {
            return (raw, npgsqlDbType);
        }

        // Check if Enum
        if (type.IsEnum)
        {
            return (Convert.ChangeType(raw, Enum.GetUnderlyingType(type)), NpgsqlDbType.Integer);
        }

        // Objects (Classes, Records, Complex Types) -> Jsonb
        if (raw is not string && type.IsClass)
        {
            try
            {
                var jsonString = JsonSerializer.Serialize(raw, DefaultJsonOptions);
                return (jsonString, NpgsqlDbType.Jsonb);
            }
            catch
            {
                return (raw, NpgsqlDbType.Unknown);
            }
        }

        return (raw, NpgsqlDbType.Unknown);
    }

    public static object? ConvertStringToNpgsqlType(object? value, NpgsqlDbType npgsqlDbType)
    {
        if (value == null || value == DBNull.Value) return DBNull.Value;

        // Intercept and handle Enum immediately, regardless of npgsqlDbType 
        if (value.GetType().IsEnum)
        {
            return Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()));
        }

        if (value is not string && (npgsqlDbType == NpgsqlDbType.Json || npgsqlDbType == NpgsqlDbType.Jsonb ||
                                    npgsqlDbType == NpgsqlDbType.Text))
        {
            return JsonSerializer.Serialize(value, DefaultJsonOptions);
        }

        if (value is not string stringValue)
            return value;
        if (npgsqlDbType == NpgsqlDbType.Unknown)
            return value;

        // Lookup DbType from static cache without allocating NpgsqlParameter
        if (!NpgsqlDbTypeToDbTypeCache.TryGetValue(npgsqlDbType, out var adoNetType))
        {
            var tempParam = new NpgsqlParameter { NpgsqlDbType = npgsqlDbType };
            adoNetType = tempParam.DbType;
            NpgsqlDbTypeToDbTypeCache.TryAdd(npgsqlDbType, adoNetType);
        }

        Type targetType = adoNetType switch
        {
            DbType.Guid => typeof(Guid),
            DbType.Int32 => typeof(int),
            DbType.Int64 => typeof(long),
            DbType.Boolean => typeof(bool),
            DbType.DateTime => typeof(DateTime),
            DbType.DateTimeOffset => typeof(DateTimeOffset),
            DbType.Date => typeof(DateOnly),
            DbType.Time => typeof(TimeOnly),
            DbType.Decimal => typeof(decimal),
            DbType.Double => typeof(double),
            DbType.Single => typeof(float),
            DbType.Byte => typeof(byte),
            _ => typeof(string)
        };

        try
        {
            var converter = TypeDescriptor.GetConverter(targetType);
            return converter.CanConvertFrom(typeof(string))
                ? converter.ConvertFromString(stringValue)
                : Convert.ChangeType(stringValue, targetType);
        }
        catch
        {
            return value;
        }
    }

    /// <summary>
    /// Universal conversion of database values (from DataReader or ObjectBlock) to ANY .NET target type.
    /// Delegates to NpOnTypeConverter for cross-driver standardization.
    /// </summary>
    public static object? ConvertDbValue(object? dbVal, Type targetType)
    {
        return Common.Extensions.NpOn.ICommonDb.DbResults.Extensions.NpOnTypeConverter.ConvertDbValue(dbVal, targetType);
    }
}
