using System.Collections.Concurrent;
using System.ComponentModel;
using System.Data;
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
    }

    public static object? NormalizePostgresValue(this object? value)
    {
        return value;
    }

    public static (object? Value, NpgsqlDbType DbType) NormalizeForNpgsql(object? raw)
    {
        if (raw == null || raw == DBNull.Value)
            return (DBNull.Value, NpgsqlDbType.Unknown);

        // DateTime handling logic specific to Postgres 6.0+ (requires UTC for Timestamptz)
        if (raw is DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Unspecified)
            {
                return (DateTime.SpecifyKind(dt, DateTimeKind.Utc), NpgsqlDbType.TimestampTz);
            }

            if (dt.Kind == DateTimeKind.Local)
            {
                return (dt.ToUniversalTime(), NpgsqlDbType.TimestampTz);
            }

            return (dt, NpgsqlDbType.TimestampTz);
        }

        var type = raw.GetType();

        // Fast lookup for cached / primitive types without allocating NpgsqlParameter
        if (!TypeToNpgsqlDbTypeCache.TryGetValue(type, out var npgsqlDbType))
        {
            // Compute once and cache for future threads
            var tempParam = new NpgsqlParameter { Value = raw };
            npgsqlDbType = tempParam.NpgsqlDbType;
            TypeToNpgsqlDbTypeCache.TryAdd(type, npgsqlDbType);
        }

        // Check if it's not a traditional type (Inferred as Text/Unknown/Object) and not a string
        if (raw is not string
            && !type.IsEnum
            && type is { IsPrimitive: false, IsValueType: false }
            && (npgsqlDbType == NpgsqlDbType.Unknown || npgsqlDbType == NpgsqlDbType.Text))
        {
            try
            {
                var jsonString = JsonMode.ToJson(raw);
                return (jsonString, NpgsqlDbType.Jsonb);
            }
            catch
            {
                return (raw, npgsqlDbType);
            }
        }

        return (raw, npgsqlDbType);
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
            return value.ToString();
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
}
