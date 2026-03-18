using System.ComponentModel;
using System.Data;
using Common.Extensions.NpOn.CommonMode;
using Npgsql;
using NpgsqlTypes;

namespace Common.Infrastructures.NpOn.PostgresExtCm.Results;

public static class PostgresUtils
{
    public static object? NormalizePostgresValue(this object? value)
    {
        // if (value is DateTime { Kind: DateTimeKind.Utc } dt) // timestamptz (offset)
        //     return dt.ToLocalTime();
        return value;
    }

    public static (object? Value, NpgsqlDbType DbType) NormalizeForNpgsql(object? raw)
    {
        if (raw == null || raw == DBNull.Value)
            return (DBNull.Value, NpgsqlDbType.Unknown);

        // Infer NpgsqlParameter 
        // Driver -> FindDataTypeName
        var p = new NpgsqlParameter { Value = raw };
        var npgsqlDbType = p.NpgsqlDbType;

        // DateTime handling logic specific to Postgres 6.0+ (requires UTC for Timestamptz)
        if (raw is DateTime dt)
        {
            // Npgsql 6.0+ default: Kind.Utc -> TimestampTz, Kind.Unspecified/Local -> Timestamp
            // If we want to force everything to Timestamptz (UTC)
            if (dt.Kind == DateTimeKind.Unspecified)
            {
                return (DateTime.SpecifyKind(dt, DateTimeKind.Utc), NpgsqlDbType.TimestampTz);
            }

            if (dt.Kind == DateTimeKind.Local)
            {
                return (dt.ToUniversalTime(), NpgsqlDbType.TimestampTz);
            }

            // Already Utc
            return (dt, NpgsqlDbType.TimestampTz);
        }

        // For Json/Jsonb: If p.NpgsqlDbType still returns Text/Unknown, 
        // we could check the type name of 'raw' (JObject, JsonDocument...)
        // But usually Npgsql handles this via Resolver Factories

        // Check if it's not a traditional type (Inferred as Text/Unknown/Object) and not a string
        var type = raw.GetType();
        if (raw is not string
            && !type.IsEnum
            && type is { IsPrimitive: false, IsValueType: false }
            && (npgsqlDbType == NpgsqlDbType.Unknown || npgsqlDbType == NpgsqlDbType.Text || p.DbType == DbType.Object))
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
        // mapping pre build command param 
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

        var p = new NpgsqlParameter { NpgsqlDbType = npgsqlDbType };
        var adoNetType = p.DbType;

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