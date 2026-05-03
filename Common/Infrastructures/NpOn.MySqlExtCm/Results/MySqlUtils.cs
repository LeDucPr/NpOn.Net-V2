using System.ComponentModel;
using System.Data;
using Common.Extensions.NpOn.CommonMode;
using MySqlConnector;
using MySqlConnector;

namespace Common.Infrastructures.NpOn.MySqlExtCm.Results;

public static class MySqlUtils
{
    public static object? NormalizeMySqlValue(this object? value)
    {
        // if (value is DateTime { Kind: DateTimeKind.Utc } dt) // timestamptz (offset)
        //     return dt.ToLocalTime();
        return value;
    }

    public static (object? Value, MySqlDbType DbType) NormalizeForMySqlConnector(object? raw)
    {
        if (raw == null || raw == DBNull.Value)
            return (DBNull.Value, MySqlDbType.Null);

        // Infer MySqlParameter 
        // Driver -> FindDataTypeName
        var p = new MySqlParameter { Value = raw };
        var npgsqlDbType = p.MySqlDbType;

        // DateTime handling logic specific to MySql 6.0+ (requires UTC for Timestamptz)
        if (raw is DateTime dt)
        {
            // MySqlConnector 6.0+ default: Kind.Utc -> TimestampTz, Kind.Unspecified/Local -> Timestamp
            // If we want to force everything to Timestamptz (UTC)
            if (dt.Kind == DateTimeKind.Unspecified)
            {
                return (DateTime.SpecifyKind(dt, DateTimeKind.Utc), MySqlDbType.Timestamp);
            }

            if (dt.Kind == DateTimeKind.Local)
            {
                return (dt.ToUniversalTime(), MySqlDbType.Timestamp);
            }

            // Already Utc
            return (dt, MySqlDbType.Timestamp);
        }

        // For Json/Jsonb: If p.MySqlDbType still returns Text/Unknown, 
        // we could check the type name of 'raw' (JObject, JsonDocument...)
        // But usually MySqlConnector handles this via Resolver Factories

        // Check if it's not a traditional type (Inferred as Text/Unknown/Object) and not a string
        var type = raw.GetType();
        if (raw is not string
            && !type.IsEnum
            && type is { IsPrimitive: false, IsValueType: false }
            && (npgsqlDbType == MySqlDbType.VarChar || npgsqlDbType == MySqlDbType.Text || p.DbType == DbType.Object))
        {
            try
            {
                var jsonString = JsonMode.ToJson(raw);
                return (jsonString, MySqlDbType.JSON);
            }
            catch
            {
                return (raw, npgsqlDbType);
            }
        }

        return (raw, npgsqlDbType);
    }

    public static object? ConvertStringToMySqlConnectorType(object? value, MySqlDbType npgsqlDbType)
        // mapping pre build command param 
    {
        if (value == null || value == DBNull.Value) return DBNull.Value;

        // Intercept and handle Enum immediately, regardless of npgsqlDbType 
        if (value.GetType().IsEnum)
        {
            return Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType()));
        }

        if (value is not string && (npgsqlDbType == MySqlDbType.JSON || npgsqlDbType == MySqlDbType.Text))
        {
            return value.ToString();
        }

        if (value is not string stringValue)
            return value;
        if (npgsqlDbType == MySqlDbType.VarChar)
            return value;

        var p = new MySqlParameter { MySqlDbType = npgsqlDbType };
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